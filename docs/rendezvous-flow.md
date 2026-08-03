# Rendezvous UX 흐름

## 한 줄 요약

**접선(Rendezvous)에서 최대 4명까지 신원 확인/Nobody 등록 → 별도 버튼으로 Station 진입 → 미션 목록 조회·선택.**

신원 확인 성공이나 Nobody 추가만으로 Station으로 넘어가지 않는다.  
**Station 진입은 접선 화면의 전용 버튼**으로만 한다.

## 용어

| 용어 | 의미 |
|------|------|
| **Rendezvous** | 요원이 외부에서 다른 요원·본부와 **접선**하는 거점 씬 |
| **Clearance** | 콜사인으로 **신원 확인** 후 **파티에 등록** (개인 프로필) |
| **Nobody** | 계정 없는 친구용 **Guest 콜사인**. 파티에만 추가. **시스템 기본값** |
| **Party** | 접선에 등록된 에이전트 목록 (**최대 4명**) |
| **Station 진입** | 파티가 1명 이상일 때, 접선 화면의 **별도 버튼**으로만 전환 |
| **Station** | 미션 목록 조회·선택 UI |
| **BDS Check** | Clearance·Station 공통 우측 상단 특수 버튼 |

## 필수 흐름

```
Boot
  └─ Rendezvous (접선) 화면
        ├─ 콜사인 → 신원 확인 → 파티에 추가 (자동으로 Station 이동 ❌)
        ├─ Nobody 추가 → 파티에 추가 (자동으로 Station 이동 ❌)
        ├─ 파티 목록 표시 (1~4명)
        ├─ [Station 진입] ← 파티 ≥1 일 때만 활성
        │         ↓
        ├─ Station — 미션 목록 조회 → 선택
        │
        │  ※ 우측 상단 [BDS Check] — 접선·Station 어디서나
        └─ Mission 수행 → Station 복귀
```

### 규칙

1. 접선 화면에서 에이전트를 **최대 4명**까지 등록한다.
2. Clearance / Nobody는 **파티 등록만** 한다. Station으로 자동 전환하지 않는다.
3. **Station 진입** 버튼으로만 Station으로 간다 (파티 1명 이상).
4. Nobody는 처음 게임을 접해 **계정이 없는 친구**가 쓰는 Guest 콜사인이다.
5. Nobody는 개인 설정을 불러오지 않고 **시스템 기본값**으로 플레이한다.
6. 접선 해제(클리어런스 해제) 시 파티를 비우고 접선 화면으로 돌아온다.

## 단계 1 — 접선 (Clearance / Nobody 등록)

Station·미션 UI는 숨긴다. 이 화면에서만 파티를 구성한다.

| 요소 | 설명 |
|------|------|
| 콜사인 입력 | 에이전트 닉네임 |
| 신원 확인 | `POST /auth/login` 등 → **파티에 추가** (Station 이동 없음) |
| **Nobody 추가** | 계정 없는 Guest → **파티에 추가** (Station 이동 없음) |
| 파티 목록 | 세로 **2/3** 지점, 가로 **90%**, 높이 **20%** 반투명 테두리 바. 등록된 멤버만 가운데 정렬 (카운트 텍스트 없음) |
| **Station 진입** | 파티 ≥1 일 때 활성. Station으로 전환하는 **유일한** 버튼 |
| BDS Check | 우측 상단 — 신원과 무관하게 센서 점검 |

### Nobody (Guest) — 4인 플레이 맥락

이 게임은 **최대 4명**이 로그인(접선)해 함께 플레이한다.

- 친구 중 한 명이 **처음**이라 계정이 없으면 **Nobody** 콜사인으로 파티에 넣는다.
- Nobody는 “바로 Station 가기” 숏컷이 **아니다**. 다른 요원과 같이 접선 목록에 오른 뒤, 함께 **Station 진입**한다.
- 설정·장비는 **시스템 기본값**만 사용한다 (`AgentSession.UsesSystemDefaults`).

| | 정식 콜사인 (Clearance) | Nobody (Guest) |
|--|------------------------|----------------|
| 등록 결과 | 파티 슬롯 추가 | 파티 슬롯 추가 |
| Station 자동 이동 | ❌ | ❌ |
| 설정 | 개인 프로필(예정) | 시스템 기본값 |
| 용도 | 계정 있는 요원 | 계정 없는 첫 플레이 친구 |

콜사인 칸에 `Nobody`를 입력해도 Nobody 추가로 처리한다.

## 단계 2 — Station (미션 목록 · 선택)

**Station 진입** 버튼으로만 표시.

| 영역 | 역할 |
|------|------|
| **미션 목록** | 카탈로그 조회 후 리스트 |
| **미션 선택** | 목록에서 선택 후 실행 |
| **스테이션 패널** | 파티 요약 (Nobody 포함 여부) |
| **클리어런스 해제** | 파티 해체 → 접선 화면 |
| **종료** | 앱 종료 |

### BDS Check (공통 HUD)

| 항목 | 내용 |
|------|------|
| 표시명 | **BDS Check** (+ 아이콘) |
| 위치 | 우측 상단 |
| 기능 | BDS 4점 교정·발사 테스트 |
| 권한 | 접선·Station 모두. 파티 없어도 시스템 기본 프로필로 실행 가능 |

## 단계 3 — 미션 수행

- Core는 활성 미션에 `InputHit`만 전달한다.
- Nobody 슬롯이 대표(첫 슬롯)이면 시스템 기본 `MissionConfig`를 쓴다.
- 종료 후 Station으로 복귀 (파티 유지).

## 관련 코드

| 경로 | 역할 |
|------|------|
| `Assets/Scenes/Rendezvous.unity` | 접선 / Station / BDS Check |
| `Assets/Core/Runtime/AgentSession.cs` | 최대 4인 파티, Station 진입 플래그 |
| `Assets/Core/Runtime/Lobby/StartMenuUI.cs` | 등록 ↔ Station 진입 분리 |
| `Assets/Core/Runtime/PinkSoftApiClient.cs` | login / catalog / complete |
| `Assets/Core/Runtime/BdsCalibrationLauncher.cs` | BDS Check |

## 비범위

- Addressables 미션 번들 동적 로드는 사용하지 않는다.
- 미션 실행은 외부 실행파일(또는 추후 정의 패키징)로 연동한다.
