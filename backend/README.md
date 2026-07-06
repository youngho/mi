# PinkSoft Backend API

## 실행 (메모리 DB)

```bash
cd backend
npm install
USE_MEMORY_DB=1 npm start
```

## MariaDB

```bash
mysql -u root -p < schema.sql
export USE_MEMORY_DB=0
export DB_HOST=localhost DB_USER=pinksoft DB_PASSWORD=pinksoft DB_NAME=pinksoft
npm start
```

## 테스트

```bash
npm test
```

## 엔드포인트

| Method | Path | 설명 |
|--------|------|------|
| POST | `/auth/login` | JWT 발급 |
| GET | `/missions/catalog` | 미션 목록 |
| POST | `/mission/complete` | 완료 + 서버 점수 검증 |
| GET | `/ranking/:missionId` | 랭킹 |

점수 검증 로직: `src/scoreValidator.js` — 클라이언트 `eventLog`를 재계산해 `finalScore`와 비교합니다.
