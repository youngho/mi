const SCORE_WEIGHTS = {
  TargetHit: 100,
  Combo: 50,
  TimeBonus: 200,
  ObjectiveComplete: 500,
  Penalty: -50,
};

const MAX_EVENTS = 500;
const MAX_SCORE = 1_000_000;
const MIN_PLAY_TIME = 3;
const MAX_PLAY_TIME = 3600;

function recalculateScore(eventLog, difficultyLevel = 2) {
  const diffMul = difficultyLevel === 1 ? 0.8 : difficultyLevel === 3 ? 1.5 : 1;
  let score = 0;
  let combo = 0;

  for (const ev of eventLog) {
    const base = SCORE_WEIGHTS[ev.eventType] ?? 0;
    if (ev.eventType === 'TargetHit') {
      combo++;
      if (combo >= 3) score += SCORE_WEIGHTS.Combo * diffMul;
    } else if (ev.eventType === 'Penalty') {
      combo = 0;
    }
    score += base * diffMul;
    score = Math.max(0, score);
  }
  return Math.round(score);
}

function validateMissionComplete(body) {
  const errors = [];
  if (!body.missionId) errors.push('missionId required');
  if (typeof body.finalScore !== 'number') errors.push('finalScore required');
  if (typeof body.playTime !== 'number') errors.push('playTime required');
  if (!Array.isArray(body.eventLog)) errors.push('eventLog required');

  if (body.playTime < MIN_PLAY_TIME || body.playTime > MAX_PLAY_TIME)
    errors.push('playTime out of range');

  if (body.eventLog && body.eventLog.length > MAX_EVENTS)
    errors.push('eventLog too large');

  if (body.finalScore < 0 || body.finalScore > MAX_SCORE)
    errors.push('finalScore out of range');

  return errors;
}

function validateScore(body) {
  const validationErrors = validateMissionComplete(body);
  if (validationErrors.length > 0)
    return { valid: false, errors: validationErrors, serverScore: 0 };

  const serverScore = recalculateScore(body.eventLog);
  const tolerance = Math.max(50, serverScore * 0.05);
  const valid = Math.abs(serverScore - body.finalScore) <= tolerance;

  return {
    valid,
    serverScore,
    errors: valid ? [] : [`score mismatch: client=${body.finalScore} server=${serverScore}`],
  };
}

function calculateStars(score, targetScore) {
  if (score >= targetScore * 1.5) return 3;
  if (score >= targetScore) return 2;
  if (score >= targetScore * 0.5) return 1;
  return 0;
}

module.exports = {
  SCORE_WEIGHTS,
  recalculateScore,
  validateMissionComplete,
  validateScore,
  calculateStars,
};
