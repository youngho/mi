# PinkSoft Mission System (PMS) 기획서

## 외부 확장형 미션 구조 및 API 명세

본 문서는 **PinkSoft**에서 개발하는 유니티 기반 모바일 게임 플랫폼의 핵심인 **'미션 및 사용자 관리 시스템'**의 아키텍처 및 기획 사양을 정의합니다. 본 시스템은 스크린 골프(골프존)의 코스 선택 방식을 벤치마킹하여, 메인 플랫폼(Core)과 외부 모듈(Mission)을 완전 분리하고 공통 API를 통해 누구나 미션을 제작 및 확장할 수 있는 플러그인 구조를 지향합니다.

---

## 1. 시스템 아키텍처 개요 (Architecture Overview)

전체 시스템은 **Core 플랫폼**과 **동적 미션 모듈**의 2계층 구조로 분리되어 구동됩니다.

```
+--------------------------------------------------------------------------+
| PinkSoft Core (메인 게임)                                                |
| - 유저 세션 및 데이터 관리 (UserData)                                    |
| - 중앙 로비 UI / 미션 브라우저 및 스크롤 뷰                             |
| - 보안 및 백엔드 API 통신 / 글로벌 랭킹 및 데이터 저장 (MariaDB)         |
| - 에셋 번들 및 어드레서블 자산 동적 로더                                 |
+--------------------------------------------------------------------------+
          │
(공통 Interface & API 계약)
          │
          ▼
+--------------------------------------------------------------------------+
| Dynamic Mission Modules (외부 미션)                                      |
| - 독립된 프리팹(Prefab) 또는 에셋 번들 (.bundle)                         |
| - 미션 고유의 맵 디자인, 오브젝트 배치 및 연출 기믹                      |
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

외부에서 제작된 모든 미션의 루트(Root) 오브젝트는 반드시 아래 인터페이스를 구현하는 컴포넌트를 포함해야 합니다. Core는 이 인터페이스를 통해서만 미션을 제어합니다.

```csharp
namespace PinkSoft.MissionSDK
{
    /// <summary>
    /// 외부 미션과 Core 플랫폼을 연결하는 공통 인터페이스
    /// </summary>
    public interface IMissionController
    {
        // 1. 미션 초기화 (Core가 미션 로드 직후 호출하여 유저 정보 및 세팅 전달)
        void InitializeMission(RuntimeUserData userData, MissionConfig config);

        // 2. 미션 상태 변경에 따른 Core 알림 이벤트 (C# 액션)
        System.Action<int> OnScoreChanged { get; set; }               // 점수 변동 시 호출 (실시간 UI 반영)
        System.Action<bool, MissionResultData> OnMissionEnded { get; set; } // 미션 종료 시 호출 (성공/실패 여부 및 결과)
    }

    [System.Serializable]
    public class RuntimeUserData
    {
        public string userId;
        public string nickname;
        public int currentLevel;
        public EquipmentStats equipment; // 유저가 장착한 장비 능력치 데이터
    }

    [System.Serializable]
    public class MissionConfig
    {
        public int difficultyLevel;      // 선택된 난이도 (1: Easy, 2: Normal, 3: Hard)
        public string weatherCondition;  // 환경 조건 (맑음, 강풍, 비 등)
    }

    [System.Serializable]
    public class MissionResultData
    {
        public int finalScore;
        public int playTime;
        public int starsEarned;          // 획득한 별 개수 (1~3개)
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

1. **점수 검증 주도권 (Core Auth):** 외부 미션이 자체적으로 최종 점수를 판단해 서버에 직접 올리는 방식은 해킹 위험이 큽니다. 미션 내부에서 특정 이벤트(예: 타겟 적중, 미션 오브젝트 상호작용) 발생 시 OnScoreChanged 이벤트를 발생시키고, 점수 계산과 누적 로직은 메인 Core 시스템이 담당합니다.
2. **클리어 및 보상 지급:** 미션이 종료되면 Core 시스템이 백엔드 API(`api.pinksoft.io/mission/complete`)를 호출하여 유저 데이터베이스(MariaDB)의 골드 보상, 경험치 및 글로벌 랭킹 점수를 안전하게 갱신합니다.

## 5. 단계별 개발 로드맵 (Roadmap)

- **Phase 1: 모놀리식 코어 개발 (내부 검증)**
  - 인터페이스(IMissionController) 구조를 먼저 선언한 뒤, 메인 프로젝트 내부에서 2~3개의 미션을 직접 구현하며 규격을 정교화합니다.
- **Phase 2: 자산 분리 및 동적 로드 테스트 (에셋 번들화)**
  - 내부 미션 맵을 유니티 에셋 번들(Asset Bundle)로 빌드하여 외부 파일 형태로 분리하고, 런타임에 이를 동적으로 읽어와 정상 플레이 및 점수 전송이 가능한지 검증합니다.
- **Phase 3: PinkSoft Mission SDK 배포**
  - 외부 개발자가 미션을 제작할 수 있도록 공통 스크립트 파일, 가이드 문서, 기본 템플릿 프로젝트를 패키지화하여 배포 환경을 구축합니다.
