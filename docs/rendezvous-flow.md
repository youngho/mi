# Rendezvous UX 흐름

## 한 줄 요약

**Rendezvous에서 신원 확인(Clearance) → Station에서 미션 목록을 조회·선택 → 미션 수행.**

로그인(클리어런스) 없이 Station·미션 목록에 접근할 수 없다.

## 용어

| 용어 | 의미 |
|------|------|
| **Rendezvous** | 요원이 외부에서 다른 요원·본부와 **접선**하는 거점 씬 (`Rendezvous.unity`) |
| **Clearance** | 접선 직후 **신원 확인** (Agent Clearance). Station 진입 게이트 |
| **Station** | 클리어런스 이후 작전 운용 UI. **미션 목록 조회·선택**, 센서 설정 |

## 필수 흐름

```
Boot
  └─ Rendezvous 씬
        ├─ [1] Clearance — 콜사인으로 신원 확인
        │         ↓ (성공 시에만)
        ├─ [2] Station — 미션 목록 조회 → 미션 선택
        │         │         (+ 센서 설정 / 클리어런스 해제)
        │         ↓
        └─ [3] Mission — 선택 미션 수행
                  ↓
               Station 복귀 (세션 유지)
```

### 규칙

1. Rendezvous에 들어오면 **먼저 Clearance**만 보인다.
2. Clearance 성공 후 `AgentSession`에 에이전트가 저장된다.
3. **Station**에서만 미션 목록을 조회하고 선택할 수 있다.
4. 미션 종료 후 Station으로 돌아와 다시 목록에서 고를 수 있다.
5. 클리어런스 해제 시 Clearance 화면으로 돌아간다.

## 단계 1 — Clearance (신원 확인)

Station·미션 UI는 숨긴다.

| 요소 | 설명 |
|------|------|
| 콜사인 입력 | 에이전트 닉네임 (`POST /auth/login`) |
| 확인 | 서버 JWT 또는 오프라인 클리어런스 |
| 실패 | Station으로 진행하지 않음 |

- **온라인:** `PinkSoftApiClient.Login` → token / userId
- **오프라인(개발용):** `local:{callsign}` — `StartMenuUI.allowOfflineClearance`

## 단계 2 — Station (미션 목록 · 선택)

Clearance 성공 후에만 표시.

| 영역 | 역할 |
|------|------|
| **미션 목록** | 카탈로그 조회 (`GET /missions/catalog` 등) 후 리스트 표시 |
| **미션 선택** | 목록에서 미션을 고르고 실행 (외부 Unity 실행파일 연동 예정) |
| **센서 설정** | BDS 4점 교정·발사 테스트 |
| **스테이션 패널** | 에이전트 신원, 교정 상태, 미션 요약 |
| **클리어런스 해제** | Clearance 화면으로 복귀 |
| **종료** | 앱 종료 |

미션 목록 없이 바로 “시작”만 두는 UI는 허용하지 않는다.  
Station의 핵심은 **조회 → 선택**이다.

## 단계 3 — 미션 수행

- Core는 활성 미션에 `InputHit`만 전달한다.
- 종료 후 `POST /mission/complete` 등으로 보고하고 **Station**으로 복귀한다.

## 관련 코드

| 경로 | 역할 |
|------|------|
| `Assets/Scenes/Rendezvous.unity` | Clearance / Station Canvas |
| `Assets/Core/Runtime/AgentSession.cs` | 클리어런스 세션 |
| `Assets/Core/Runtime/Lobby/StartMenuUI.cs` | Clearance ↔ Station 전환 |
| `Assets/Core/Runtime/PinkSoftApiClient.cs` | login / catalog / complete |
| `Assets/Core/Runtime/BdsCalibrationLauncher.cs` | Station → 센서 설정 |

## 비범위

- Addressables 미션 번들 동적 로드는 사용하지 않는다.
- 미션 실행은 외부 실행파일(또는 추후 정의 패키징)로 연동한다.
