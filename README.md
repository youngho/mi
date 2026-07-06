# PinkSoft Mission System (PMS) 기획서

> **구현 산출물:** [개발 로드맵](docs/roadmap.md) · [BDS Go/No-Go](docs/bds-gonogo-checklist.md) · [Mission SDK v1](docs/mission-sdk-v1.md) · [API 명세](docs/api-openapi.yaml) · [Addressables 결정](docs/decisions/addressables.md)

## 프로젝트 구조

```
mi/                    # 모노레포 루트 = Unity 프로젝트
├── Assets/            # BDS, Core, MissionSDK, Missions, Scenes
├── ProjectSettings/
├── Packages/
├── docs/              # 세분화 로드맵, SDK/API 명세
├── backend/           # REST API + MariaDB 스키마 + 점수 검증
├── tools/bds-capture/ # Go/No-Go용 LiDAR 녹화 CLI
└── tests/             # BDS 단위 테스트
```

**MVP 우선순위:** BDS 하드웨어 검증 → 단일 미션 프로토타입 → PMS 플랫폼

## 구현 변경 요약 (BDS-PMS 통합)

BDS(Bullet Detection System)는 **PMS Core에 상주**하며, 미션 번들에는 가공된 적중 좌표(`InputHit`)만 전달합니다.

### 런타임 구조

```
BdsService (Core 상주, DontDestroyOnLoad)
  → IInputSource (BDS / Touch / Debug)
    → MissionInputRouter (활성 미션 1개에만 라우팅)
      → IMissionInput (MissionContext.Input)
        → IMissionController (미션 번들)
          → ReportEvent → ScoreEngine (Core)
```

### 주요 변경

| 항목 | 내용 |
|------|------|
| 입력 계약 | `InputHit`, `IMissionInput`을 MissionSDK로 이동. 외부 미션은 BDS 어셈블리 미참조 |
| 미션 초기화 | `InitializeMission(user, MissionContext)` — Core가 `Input`·`Config` 주입 |
| Core 서비스 | `BdsService` (LiDAR·필터·교정), `MissionInputRouter` (입력 라우팅) |
| 교정·센서 테스트 | `BdsCalibrationMode` — PMS 시스템 모드 (4점 교정 + 발사 테스트) |
| 내장 미션 | `TargetPracticeMission`, `TimedEscapeMission`, `ComboShootMission` — Context.Input 구독 |
| 백엔드 | `POST /auth/login`, `GET /missions/catalog`, `POST /mission/complete`, `GET /ranking/:id` |

### Unity 핵심 파일

| 경로 | 역할 |
|------|------|
| `Assets/MissionSDK/Runtime/InputTypes.cs` | `InputHit`, `MissionContext`, `IMissionInput` |
| `Assets/Core/Runtime/BdsService.cs` | BDS lifecycle 싱글톤 |
| `Assets/Core/Runtime/MissionInputRouter.cs` | 활성 미션 입력 라우터 |
| `Assets/Core/Runtime/MissionSessionController.cs` | 미션 세션·점수 브리지 |
| `Assets/Core/Runtime/Modes/BdsCalibrationMode.cs` | BDS 교정·발사 테스트 시스템 모드 |
| `Assets/Core/Runtime/BdsCalibrationLauncher.cs` | 로비 → 센서 설정 모드 진입 |
| `Assets/Core/Runtime/Lobby/LobbyCalibrationUI.cs` | 로비 진입 버튼 |
| `Assets/BDS/Runtime/` | LiDAR 파서·필터 (Core 전용, 미션 미포함) |

### 씬 구성 권장

1. **Boot** — `BdsService`, `MissionInputRouter`, `MissionSessionController`
2. **Lobby** — `BdsCalibrationLauncher`, 미션 카탈로그
3. **Mission** — 번들 미션만 배치 (BDS/교정 UI 없음)

상세 스펙: [Mission SDK v1](docs/mission-sdk-v1.md) · Unity 가이드: [unity/PinkSoft/README.md](unity/PinkSoft/README.md) (→ 레포 루트에서 Hub로 열기)

---

## 외부 확장형 미션 구조 및 API 명세

본 문서는 **PinkSoft**에서 개발하는 유니티 기반 모바일 게임 플랫폼의 핵심인 **'미션 및 사용자 관리 시스템'**의 아키텍처 및 기획 사양을 정의합니다. 본 시스템은 스크린 골프(골프존)의 코스 선택 방식을 벤치마킹하여, 메인 플랫폼(Core)과 외부 모듈(Mission)을 완전 분리하고 공통 API를 통해 누구나 미션을 제작 및 확장할 수 있는 플러그인 구조를 지향합니다.

---

## 1. 시스템 아키텍처 개요 (Architecture Overview)

전체 시스템은 **Core 플랫폼**과 **동적 미션 모듈**의 2계층 구조로 분리되어 구동됩니다.

```
+--------------------------------------------------------------------------+
| PinkSoft Core (메인 게임)                                                |
| - 유저 세션 및 데이터 관리 (UserData)                                    |
| - BDS (LiDAR·필터·교정) — BdsService 상주                                |
| - MissionInputRouter — 활성 미션에 InputHit 라우팅                       |
| - 중앙 로비 UI / 미션 브라우저 및 스크롤 뷰                             |
| - 보안 및 백엔드 API 통신 / 글로벌 랭킹 및 데이터 저장 (MariaDB)         |
| - Addressables 미션 패키지 동적 로더                                     |
+--------------------------------------------------------------------------+
          │
(공통 Interface & API 계약)
          │
          ▼
+--------------------------------------------------------------------------+
| Dynamic Mission Modules (외부 미션)                                      |
| - 독립된 프리팹(Prefab) 또는 Addressables 미션 패키지                    |
| - MissionContext.Input(IMissionInput)으로 적중 좌표 수신                 |
| - Raycast 판정 후 ReportEvent — BDS/LiDAR 코드 미포함                    |
| - IMissionController 표준 규격 준수                                      |
+--------------------------------------------------------------------------+
```

---

## 2. 데이터 구조 및 인터페이스 규격

### 2.1 미션 메타데이터 규격 (JSON)

로비 화면에서 미션 리스트를 가볍게 브라우징하기 위해 사용하는 메타데이터 형식입니다. 외부 개발자는 미션 에셋 파일과 함께 아래 형식의 JSON 정보를 제공해야 합니다.

```json
{
  "missionId": "ext_mission_spy_01",
  "title": "잠입: 연구소 탈출",
  "description": "경비병의 시야를 피해 제한 시간 내에 연구소를 탈출하세요.",
  "author": "Developer_Pink",
  "version": "1.0.0",
  "bundleUrl": "https://api.pinksoft.io/missions/spy_01.bundle",
  "requiredLevel": 5,
  "entryFee": 500,
  "timeLimit": 180,
  "targetScore": 5000
}
```

### 2.2 핵심 C# 인터페이스 (Core API)

> **최신 스펙:** [Mission SDK v1](docs/mission-sdk-v1.md) — BDS는 Core(`BdsService`)에 상주하고, 미션에는 `MissionContext.Input`으로 가공된 `InputHit`만 주입합니다.

외부에서 제작된 모든 미션의 루트(Root) 오브젝트는 반드시 아래 인터페이스를 구현하는 컴포넌트를 포함해야 합니다. Core는 이 인터페이스를 통해서만 미션을 제어합니다.

```csharp
namespace PinkSoft.MissionSDK
{
    public interface IMissionController
    {
        void InitializeMission(RuntimeUserData userData, MissionContext context);
        void OnPause();
        void OnResume();
        void Shutdown();
        void ReportEvent(ScoreEventType eventType, string targetId);

        event Action<int> OnScoreChanged;
        event Action<bool, MissionResultData> OnMissionEnded;
        event Action<MissionError> OnError;
    }

    public class MissionContext
    {
        public IMissionInput Input;  // Core MissionInputRouter
        public MissionConfig Config;
    }
}
```

## 3. UI/UX 화면 흐름 기획

### 단계 1: 미션 로비 (Mission Browser)

- **스크린 골프 코스 선택 방식 구현:** 카테고리별 탭(공식 미션, 커뮤니티 미션 등)과 가로/세로 스크롤 뷰를 통해 미션 카드 형태로 리스트업합니다.
- **비동기 썸네일 로딩:** 텍스트 메타데이터를 먼저 로드하고, 화면에 보이는 미션의 썸네일(Sprite)만 어드레서블을 통해 동적 로드하여 로비 성능을 극대화합니다.

### 단계 2: 상세 정보 및 옵션 세팅 팝업

- 미션을 클릭하면 상세 레이어가 팝업됩니다.
- 사용자는 게임 진입 전 환경 옵션(예: 난이도 조절, 특수 환경 요소)을 세팅할 수 있으며, 이 설정값은 MissionConfig에 담겨 미션 씬으로 주입됩니다.

### 단계 3: 비동기 로딩 및 씬 진입

- '시작' 버튼 클릭 시, 해당 미션의 .bundle 파일이 로컬 캐시에 있는지 확인 후 다운로드 또는 로드를 진행합니다.
- 로딩 중에는 미션의 힌트 정보 및 썸네일을 표시합니다.

## 4. 사용자 데이터 및 점수 관리 (Security & Anti-Cheat)

1. **점수 검증 주도권 (Core Auth):** 미션은 `ReportEvent`로 이벤트만 보고하고, 점수 계산·누적은 Core `ScoreEngine`이 담당합니다. 서버는 `eventLog`를 재계산해 검증합니다.
2. **클리어 및 보상 지급:** 미션이 종료되면 Core 시스템이 백엔드 API(`api.pinksoft.io/mission/complete`)를 호출하여 유저 데이터베이스(MariaDB)의 골드 보상, 경험치 및 글로벌 랭킹 점수를 안전하게 갱신합니다.

## 5. BDS (Bullet Detection System)

BDS는 **PMS Core의 하위시스템**으로 상주합니다 (`BdsService`). LiDAR/UART/필터/교정은 Core가 독점하고, 미션 번들에는 가공된 `InputHit`(스크린 좌표 + 타임스탬프)만 `MissionInputRouter`를 통해 전달합니다.

빔프로젝터 스크린 환경에서 **고속 LiDAR**로 직경 6mm 비비탄 알갱이를 직접 감지합니다. 초고속 UART 패킷 파싱은 백그라운드 스레드에서 처리하고, 탄환 판정 결과만 메인 스레드로 전달하는 파이프라인 구조를 따릅니다.

```
[LiDAR 센서] ──(UART)──> [LidarHighSpeedReader] ──> [LidarBulletFilter]
        │
        ▼
[BdsInputSource] ──(InputHit)──> [MissionInputRouter] ──> [활성 미션 IMissionController]
```

- **교정·센서 테스트:** 로비에서 `BdsCalibrationMode` 시스템 모드 (4점 Homography + 발사 테스트)
- **모바일:** Core가 `TouchInputSource`로 교체 — 미션 코드 변경 없음

```
[LiDAR 센서] ──(UART 고속 스트리밍)──> [백그라운드 스레드] (Raw 바이트 파싱)
        │
        (탄환 매칭 좌표만 Thread-Safe Queue에 삽입)
        ▼
[BdsService / BdsInputSource] <──(Update 메인 스레드)── [Dequeue 및 InputHit 변환]
```

### 5.1 하드웨어 구성 및 설치

초고속으로 이동하는 탄환을 직접 센싱해야 하므로 샘플링 레이트(Hz)가 핵심입니다. 초기 빌드 및 상용화 단계에 맞춰 아래 센서 라인업을 호환하도록 설계합니다.

- **추천 센서 라인업:**
  - **RPLIDAR A3:** 샘플링 16,000 Hz / 측정 거리 25m (직접 감지 최소 스펙)
  - **RPLIDAR S3:** 샘플링 32,000 Hz / 측정 거리 40m (상용화 및 높은 적중률 확보용)
- **통신 프로토콜:** UART (USB 가상 컴포트 연동), 고속 보레이트(BaudRate) 대응

**설치 메커니즘 (레이저 커튼 셋팅):**

- **위치 선정:** 라이다 센서를 천 스크린의 하단(또는 상단) 모서리에 배치합니다.
- **오프셋 간격:** 라이다의 레이저 스캔 평면을 천 스크린 표면과 완전히 평행하게 맞추되, 스크린 앞쪽으로 **약 2~3cm 공간을 띄워** 레이저 평면(커튼)을 세웁니다.
- **감지 원리:** 비비탄이 천에 부딪혀 발생하는 스크린의 울렁임이나 흔들림은 노이즈로 간주하여 필터링하고, 탄환이 천을 타격하기 직전 레이저 커튼을 통과하는 찰나의 순간(1ms 미만)을 고속 빔으로 직접 맞추어 좌표를 포착합니다.

### 5.2 시리얼 파싱 전략

초당 수만 번의 고속 거리 측정 패킷이 유니티의 메인 렌더링 프레임(FPS)을 저하시키지 않도록 **멀티스레딩 및 비동기 큐(Queue)** 구조를 채택합니다.

1. **대용량 버퍼 할당:** 시리얼 포트 개방 시 ReadBufferSize를 충분히 크게 설정하여 고속 샘플링 시 데이터가 누락되는 오버플로우를 방지합니다.
2. **비동기 패킷 조립:** 백그라운드 스레드 내에서 하드웨어 고정 헤더 바이트를 상시 추적하고, 유효성 검사(Checksum/CRC)를 통과한 데이터만 순간적으로 계산 큐로 넘깁니다.
3. **데이터 정규화:** 센서 기종(A3, S3 등)에 따라 패킷당 포함된 샘플 개수가 다르므로, 파싱 단에서 이를 **\[각도(\(\theta\)), 거리(\(r\))\]** 쌍으로 정규화하여 상위 모듈로 전달합니다.

### 5.3 소프트웨어 구현 주요 모듈 (C# Scripts 구조)

#### LidarHighSpeedReader.cs (고속 통신 및 파싱)

- `System.Threading.Thread` 기반으로 구동되며, 하드웨어 패킷을 바이트 단위로 분해해 각도 분해능과 거리 수치를 연산한다.
- **기종 확장성:** 추후 센서 교체 시 패킷 구조체·프로토콜 매핑 함수만 교체할 수 있도록 `ILidarParser` 인터페이스 구조를 적용한다.

#### LidarCoordinateMapper.cs (직교좌표 변환 및 스크린 매핑)

- 극좌표계 \((r, \theta)\) 데이터를 삼각함수로 2D 직교좌표 \((X, Y)\)로 1차 변환한다.
- 라이다 설치 각도 왜곡 보정을 위해, 프로젝터 빔 화면 4개 모서리를 타격해 픽셀 좌표와 1:1 매칭하는 **Homography 매트릭스 변환**을 포함한다.

#### LidarBulletFilter.cs (초고속 탄환 격발 트리거 필터)

- **공간 마스킹 (Static Masking):** 스크린 틀, 바닥, 천장 등 정적으로 고정된 거리 데이터는 레이저 커튼 영역에서 미리 제외(Masking)한다.
- **순간 점 트리거 (Instant Point Trigger):** 비비탄 알갱이는 라이다 스캔 주기 기준 **1~2프레임** 동안만 나타났다가 다음 회전에서 사라지는 특성을 가진다.
- **판정 조건:** 정적 배경에 없던 물체가 **단 1프레임** 특정 좌표에 출현 후 소멸했을 때 비비탄 궤적으로 판정하고, 메인 스레드에 격발 이벤트 `OnBulletDetected(Vector2 screenPos)`를 발생시킨다.

### 5.4 BDS 개발 마일스톤

1. **Phase 1: 고속 시리얼 데이터 스트리밍 안정화 (1주차)**
   - 16kHz 이상의 고속 패킷 환경에서 데이터 밀림이나 버퍼 오버플로우 없이 유니티 백그라운드 스레드가 상주하며 바이트를 정상적으로 분해하는지 검증합니다.
2. **Phase 2: Point Cloud 뷰어 빌드 (1주차)**
   - 라이다가 뿌려주는 각도/거리 데이터를 유니티 화면에 2D 점(Point)들로 실시간 시각화하여 센서 앞을 무언가 지나갈 때 점이 튀는 현상을 확인합니다.
3. **Phase 3: 초고속 탄환 필터 알고리즘 구현 (2주차)**
   - 실제 비비탄을 발사하여 1프레임 미만으로 찍히는 점의 거리/강도(Confidence) 변화를 프로파일링하고, 팅겨 나간 후 천의 흔들림(지속 노이즈)을 지워버리는 컷오프(Cut-off) 필터를 적용합니다.
4. **Phase 4: 4점 교정(Calibration) 시스템 구축 (2주차)**
   - `BdsCalibrationMode`에서 프로젝터 화면 모서리 4점 Homography + 발사 테스트
5. **Phase 5: 유니티 게임 콘텐츠 연동 (3주차)**
   - `MissionInputRouter` → 미션 `ReportEvent` → Core `ScoreEngine` 파이프라인 연동 (구현: `MissionSessionController`, 내장 미션 3종)

---

## 6. 미션 시스템 개발 로드맵 (Roadmap)

- **Phase 1: 모놀리식 코어 개발 (내부 검증)**
  - 인터페이스(IMissionController) 구조를 먼저 선언한 뒤, 메인 프로젝트 내부에서 2~3개의 미션을 직접 구현하며 규격을 정교화합니다.
- **Phase 2: 자산 분리 및 동적 로드 테스트 (에셋 번들화)**
  - 내부 미션 맵을 유니티 에셋 번들(Asset Bundle)로 빌드하여 외부 파일 형태로 분리하고, 런타임에 이를 동적으로 읽어와 정상 플레이 및 점수 전송이 가능한지 검증합니다.
- **Phase 3: PinkSoft Mission SDK 배포**
  - 외부 개발자가 미션을 제작할 수 있도록 공통 스크립트 파일, 가이드 문서, 기본 템플릿 프로젝트를 패키지화하여 배포 환경을 구축합니다.
