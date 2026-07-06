# PinkSoft 개발 로드맵 (세분화)

[BDS Go/No-Go 체크리스트](bds-gonogo-checklist.md) · [Mission SDK v1](mission-sdk-v1.md) · [API 명세](api-openapi.yaml)

## Track A: BDS (우선)

| 단계 | 내용 | 산출물 | 예상 |
|------|------|--------|------|
| A-0 | Go/No-Go 실험 | 체크리스트 결과, raw 로그 | 3~5일 |
| A-1 | 시리얼 스트리밍 | `ILidarParser`, `LidarHighSpeedReader` | 1~2주 |
| A-2 | 포인트클라우드 뷰어 | `LidarPointCloudViewer`, 녹화/재생 | 1주 |
| A-3 | 탄환 필터 | `LidarBulletFilter`, 통계 리포트 | 2~4주 |
| A-4 | 4점 교정 | `HomographyCalculator`, `CalibrationManager` | 1~2주 |
| A-5 | 게임 프로토타입 | `IInputSource`, `TargetShootingPrototype` | 1~2주 |

**Track A 합계:** 6~10주

## Track B: PMS (BDS A-5 이후)

| 단계 | 내용 | 산출물 | 예상 |
|------|------|--------|------|
| B-1 | Unity 골격 | Boot/Lobby/Mission 씬 구조 | 3~5일 |
| B-2 | MissionSDK v1 | `IMissionController` 확장, 문서 | 1주 |
| B-3 | 내장 미션 + 점수 | `ScoreEngine`, 미션 3종 | 2~3주 |
| B-4 | 로비 UI | 카탈로그, 썸네일, 진입 조건 | 1~2주 |
| B-5 | 동적 로드 | `MissionBundleLoader`, Addressables | 2~3주 |
| B-6 | 백엔드 MVP | REST API, MariaDB | 2~4주 |
| B-7 | 안티치트 | 이벤트 로그, 서버 검증 | 1~2주 |
| B-8 | SDK 배포 | UPM 패키지, 템플릿 | 2~3주 |

**Track B 합계:** 10~16주

## 첫 플레이어블 목표

> BB탄(또는 터치)으로 화면 타겟 1개 맞추면 점수 오름 — 로비·백엔드·SDK 없음

## MVP 제품 범위

- **1차:** 아케이드 단일 타깃 (BDS + 단일 미션)
- **2차:** 로비 + 내장 미션 2~3개 + 로컬 점수
- **3차:** 백엔드 + 번들 + SDK
