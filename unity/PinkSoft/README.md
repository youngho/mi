# PinkSoft Unity 프로젝트

Unity **2022.3 LTS** (`2022.3.49f1` 권장). Hub에서 `unity/PinkSoft` 폴더를 열면 됩니다.

## 빠른 시작

1. Unity Hub → **Open** → `unity/PinkSoft`
2. **Boot** 씬 열기 → Play
3. Boot에서 Core 초기화 후 **Lobby** 자동 로드
4. Lobby에서 **BDS 센서 설정** 버튼 → 교정·발사 테스트

빌드 설정: `File → Build Settings`에 Boot(0), Lobby(1) 등록됨.

## 폴더

| 경로 | 역할 |
|------|------|
| `Assets/BDS/` | LiDAR 시리얼, 필터 (Core `BdsService`가 사용) |
| `Assets/Core/` | BdsService, MissionInputRouter, 점수 엔진, BdsCalibrationLauncher |
| `Assets/Core/Runtime/Modes/` | `BdsCalibrationMode` — 센서 교정·테스트 시스템 모드 |
| `Assets/MissionSDK/` | `IMissionController`, `MissionContext`, `InputHit`, `IMissionInput` |
| `Assets/Missions/` | 내장 미션 3종 |
| `Assets/Scenes/` | `Boot.unity`, `Lobby.unity` |
| `Assets/StreamingAssets/Missions/` | 카탈로그 JSON |

## 씬 권장 구성

```mermaid
flowchart LR
  Boot[Boot] --> Lobby[Lobby]
  Lobby --> Mission[Mission]
  Boot -->|DontDestroyOnLoad| BdsService[BdsService]
  BdsService --> Router[MissionInputRouter]
  Router --> MissionBundle[Mission_Bundle]
```

1. **Boot** — `BdsService` (DontDestroyOnLoad), `MissionInputRouter` 초기화
2. **Lobby** — `BdsCalibrationLauncher` (센서 설정), 미션 카탈로그
3. **Mission** — 번들 로드 미션만 배치. BDS/교정 UI 없음

## BDS + PMS 통합

| 계층 | 컴포넌트 | 역할 |
|------|----------|------|
| Core 상주 | `BdsService` | LiDAR 필터, 교정, IInputSource 선택 |
| Core 상주 | `MissionInputRouter` | 활성 미션에 InputHit 라우팅 |
| 미션 번들 | `IMissionController` | `MissionContext.Input` 구독 → Raycast → ReportEvent |

### Boot 씬 오브젝트

- `PMS_Core` — `BdsService`, `MissionInputRouter`, `MissionSessionController`, `BootSceneLoader`

### Lobby 씬

- `BdsCalibrationLauncher` + `BdsCalibrationMode` — 센서 4점 교정·발사 테스트 (시스템 모드)
- `LobbyCalibrationUI` — 위 모드 진입 버튼 (선택)

### Mission 씬

- `TargetShootingPrototype` 또는 번들 미션
- `MissionSessionController.StartMission(user, config)` 호출

## Addressables

미션 번들 빌드는 [Addressables ADR](../../docs/decisions/addressables.md)를 따릅니다.

외부 미션은 **MissionSDK만** 참조하고 BDS 어셈블리는 참조하지 않습니다.
