-- PinkSoft PMS MariaDB 스키마 (MVP)

CREATE TABLE IF NOT EXISTS users (
    id            VARCHAR(36) PRIMARY KEY,
    nickname      VARCHAR(64) NOT NULL UNIQUE,
    device_id     VARCHAR(128),
    level         INT NOT NULL DEFAULT 1,
    gold          INT NOT NULL DEFAULT 0,
    exp           INT NOT NULL DEFAULT 0,
    created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS missions (
    mission_id    VARCHAR(64) PRIMARY KEY,
    title         VARCHAR(128) NOT NULL,
    description   TEXT,
    author        VARCHAR(64),
    version       VARCHAR(16) NOT NULL,
    bundle_url    VARCHAR(512) NOT NULL,
    bundle_hash   VARCHAR(64),
    required_level INT NOT NULL DEFAULT 1,
    entry_fee     INT NOT NULL DEFAULT 0,
    time_limit    INT NOT NULL DEFAULT 180,
    target_score  INT NOT NULL DEFAULT 5000,
    category      ENUM('official', 'community') DEFAULT 'official',
    active        BOOLEAN DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS mission_results (
    id            BIGINT AUTO_INCREMENT PRIMARY KEY,
    user_id       VARCHAR(36) NOT NULL,
    mission_id    VARCHAR(64) NOT NULL,
    final_score   INT NOT NULL,
    play_time     INT NOT NULL,
    stars_earned  TINYINT NOT NULL,
    event_log     JSON NOT NULL,
    validated     BOOLEAN DEFAULT FALSE,
    created_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id),
    FOREIGN KEY (mission_id) REFERENCES missions(mission_id),
    INDEX idx_mission_score (mission_id, final_score DESC)
);

CREATE TABLE IF NOT EXISTS rankings (
    mission_id    VARCHAR(64) NOT NULL,
    user_id       VARCHAR(36) NOT NULL,
    best_score    INT NOT NULL,
    best_time     INT NOT NULL,
    updated_at    TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (mission_id, user_id),
    FOREIGN KEY (user_id) REFERENCES users(id),
    FOREIGN KEY (mission_id) REFERENCES missions(mission_id)
);

-- 시드 데이터
INSERT IGNORE INTO missions (mission_id, title, description, author, version, bundle_url, required_level, entry_fee, time_limit, target_score, category)
VALUES
    ('builtin_target_practice', '타겟 연습', '고정 타겟을 순서대로 맞추세요', 'PinkSoft', '1.0.0', 'https://cdn.pinksoft.io/missions/target_practice.bundle', 1, 0, 120, 500, 'official'),
    ('builtin_timed_escape', '제한 시간 탈출', '시간 내 탈출구에 도달하세요', 'PinkSoft', '1.0.0', 'https://cdn.pinksoft.io/missions/timed_escape.bundle', 3, 200, 90, 1000, 'official'),
    ('builtin_combo_shoot', '콤보 슈팅', '연속 적중으로 콤보를 쌓으세요', 'PinkSoft', '1.0.0', 'https://cdn.pinksoft.io/missions/combo_shoot.bundle', 5, 500, 180, 5000, 'official');
