const express = require('express');
const cors = require('cors');
const jwt = require('jsonwebtoken');
const { v4: uuidv4 } = require('uuid');
const mariadb = require('mariadb');
const { validateScore, calculateStars } = require('./scoreValidator');

const PORT = process.env.PORT || 3000;
const JWT_SECRET = process.env.JWT_SECRET || 'dev-secret-change-me';
const USE_MEMORY = process.env.USE_MEMORY_DB === '1';

const app = express();
app.use(cors());
app.use(express.json({ limit: '256kb' }));

let pool = null;
const memory = {
  users: new Map(),
  missions: [
    {
      missionId: 'builtin_target_practice',
      title: '타겟 연습',
      description: '고정 타겟을 순서대로 맞추세요',
      author: 'PinkSoft',
      version: '1.0.0',
      bundleUrl: 'https://cdn.pinksoft.io/missions/target_practice.bundle',
      requiredLevel: 1,
      entryFee: 0,
      timeLimit: 120,
      targetScore: 500,
      category: 'official',
    },
    {
      missionId: 'builtin_timed_escape',
      title: '제한 시간 탈출',
      description: '시간 내 탈출구에 도달하세요',
      author: 'PinkSoft',
      version: '1.0.0',
      bundleUrl: 'https://cdn.pinksoft.io/missions/timed_escape.bundle',
      requiredLevel: 3,
      entryFee: 200,
      timeLimit: 90,
      targetScore: 1000,
      category: 'official',
    },
    {
      missionId: 'builtin_combo_shoot',
      title: '콤보 슈팅',
      description: '연속 적중으로 콤보를 쌓으세요',
      author: 'PinkSoft',
      version: '1.0.0',
      bundleUrl: 'https://cdn.pinksoft.io/missions/combo_shoot.bundle',
      requiredLevel: 5,
      entryFee: 500,
      timeLimit: 180,
      targetScore: 5000,
      category: 'official',
    },
  ],
  results: [],
  rankings: new Map(),
};

async function getPool() {
  if (USE_MEMORY) return null;
  if (!pool) {
    pool = mariadb.createPool({
      host: process.env.DB_HOST || 'localhost',
      user: process.env.DB_USER || 'pinksoft',
      password: process.env.DB_PASSWORD || 'pinksoft',
      database: process.env.DB_NAME || 'pinksoft',
      connectionLimit: 5,
    });
  }
  return pool;
}

function authMiddleware(req, res, next) {
  const header = req.headers.authorization;
  if (!header?.startsWith('Bearer '))
    return res.status(401).json({ error: 'Unauthorized' });
  try {
    req.user = jwt.verify(header.slice(7), JWT_SECRET);
    next();
  } catch {
    res.status(401).json({ error: 'Invalid token' });
  }
}

app.post('/auth/login', async (req, res) => {
  const { nickname, deviceId } = req.body;
  if (!nickname)
    return res.status(400).json({ error: 'nickname required' });

  const userId = uuidv4();
  const db = await getPool();

  if (db) {
    const conn = await db.getConnection();
    try {
      const existing = await conn.query('SELECT id FROM users WHERE nickname = ?', [nickname]);
      let id = existing[0]?.id;
      if (!id) {
        await conn.query('INSERT INTO users (id, nickname, device_id) VALUES (?, ?, ?)', [userId, nickname, deviceId || null]);
        id = userId;
      }
      const token = jwt.sign({ userId: id, nickname }, JWT_SECRET, { expiresIn: '7d' });
      res.json({ token, userId: id, nickname });
    } finally {
      conn.release();
    }
  } else {
    let user = [...memory.users.values()].find((u) => u.nickname === nickname);
    if (!user) {
      user = { userId, nickname, gold: 0, exp: 0, level: 1 };
      memory.users.set(userId, user);
    }
    const token = jwt.sign({ userId: user.userId, nickname: user.nickname }, JWT_SECRET, { expiresIn: '7d' });
    res.json({ token, userId: user.userId, nickname: user.nickname });
  }
});

app.get('/missions/catalog', authMiddleware, async (req, res) => {
  const category = req.query.category;
  const db = await getPool();

  if (db) {
    const conn = await db.getConnection();
    try {
      let rows;
      if (category)
        rows = await conn.query('SELECT * FROM missions WHERE active = 1 AND category = ?', [category]);
      else
        rows = await conn.query('SELECT * FROM missions WHERE active = 1');
      res.json({ missions: rows.map(mapMissionRow) });
    } finally {
      conn.release();
    }
  } else {
    let missions = memory.missions;
    if (category)
      missions = missions.filter((m) => m.category === category);
    res.json({ missions });
  }
});

app.post('/mission/complete', authMiddleware, async (req, res) => {
  const { missionId, finalScore, playTime, starsEarned, eventLog } = req.body;
  const validation = validateScore({ missionId, finalScore, playTime, eventLog });
  if (!validation.valid)
    return res.status(422).json({ error: 'Validation failed', details: validation.errors });

  const db = await getPool();
  const userId = req.user.userId;

  let missionMeta = memory.missions.find((m) => m.missionId === missionId);
  const stars = starsEarned ?? calculateStars(validation.serverScore, missionMeta?.targetScore ?? 1000);
  const goldReward = Math.floor(validation.serverScore / 10);
  const expGained = validation.serverScore;

  if (db) {
    const conn = await db.getConnection();
    try {
      const missions = await conn.query('SELECT * FROM missions WHERE mission_id = ?', [missionId]);
      missionMeta = missions[0] ? mapMissionRow(missions[0]) : null;
      await conn.query(
        `INSERT INTO mission_results (user_id, mission_id, final_score, play_time, stars_earned, event_log, validated)
         VALUES (?, ?, ?, ?, ?, ?, 1)`,
        [userId, missionId, validation.serverScore, playTime, stars, JSON.stringify(eventLog)]
      );
      await conn.query(
        `INSERT INTO rankings (mission_id, user_id, best_score, best_time)
         VALUES (?, ?, ?, ?)
         ON DUPLICATE KEY UPDATE
           best_score = GREATEST(best_score, VALUES(best_score)),
           best_time = IF(VALUES(best_score) > best_score, VALUES(best_time), best_time)`,
        [missionId, userId, validation.serverScore, playTime]
      );
      await conn.query('UPDATE users SET gold = gold + ?, exp = exp + ? WHERE id = ?', [goldReward, expGained, userId]);
      const rankRows = await conn.query(
        `SELECT COUNT(*) + 1 AS r FROM rankings WHERE mission_id = ? AND best_score > (
           SELECT best_score FROM rankings WHERE mission_id = ? AND user_id = ?
         )`,
        [missionId, missionId, userId]
      );
      res.json({ goldReward, expGained, newRank: Number(rankRows[0]?.r ?? 1), validated: true });
    } finally {
      conn.release();
    }
  } else {
    memory.results.push({ userId, missionId, finalScore: validation.serverScore, playTime, stars, eventLog });
    const key = `${missionId}:${userId}`;
    const prev = memory.rankings.get(key);
    if (!prev || validation.serverScore > prev.bestScore)
      memory.rankings.set(key, { missionId, userId, bestScore: validation.serverScore, bestTime: playTime });
    const user = memory.users.get(userId) || { gold: 0, exp: 0 };
    user.gold += goldReward;
    user.exp += expGained;
    memory.users.set(userId, user);
    const better = [...memory.rankings.values()].filter((r) => r.missionId === missionId && r.bestScore > validation.serverScore).length;
    res.json({ goldReward, expGained, newRank: better + 1, validated: true });
  }
});

app.get('/ranking/:missionId', async (req, res) => {
  const limit = Math.min(parseInt(req.query.limit, 10) || 50, 100);
  const { missionId } = req.params;
  const db = await getPool();

  if (db) {
    const conn = await db.getConnection();
    try {
      const rows = await conn.query(
        `SELECT r.best_score AS score, r.best_time AS playTime, u.id AS userId, u.nickname
         FROM rankings r JOIN users u ON r.user_id = u.id
         WHERE r.mission_id = ?
         ORDER BY r.best_score DESC LIMIT ?`,
        [missionId, limit]
      );
      res.json({ entries: rows.map((row, i) => ({ rank: i + 1, userId: row.userId, nickname: row.nickname, score: row.score, playTime: row.playTime })) });
    } finally {
      conn.release();
    }
  } else {
    const entries = [...memory.rankings.values()]
      .filter((r) => r.missionId === missionId)
      .sort((a, b) => b.bestScore - a.bestScore)
      .slice(0, limit)
      .map((r, i) => {
        const user = memory.users.get(r.userId);
        return { rank: i + 1, userId: r.userId, nickname: user?.nickname ?? 'unknown', score: r.bestScore, playTime: r.bestTime };
      });
    res.json({ entries });
  }
});

function mapMissionRow(row) {
  return {
    missionId: row.mission_id ?? row.missionId,
    title: row.title,
    description: row.description,
    author: row.author,
    version: row.version,
    bundleUrl: row.bundle_url ?? row.bundleUrl,
    bundleHash: row.bundle_hash ?? row.bundleHash,
    requiredLevel: row.required_level ?? row.requiredLevel,
    entryFee: row.entry_fee ?? row.entryFee,
    timeLimit: row.time_limit ?? row.timeLimit,
    targetScore: row.target_score ?? row.targetScore,
    category: row.category,
  };
}

if (require.main === module) {
  app.listen(PORT, () => {
    console.log(`PinkSoft API listening on :${PORT} (memory=${USE_MEMORY})`);
  });
}

module.exports = app;
