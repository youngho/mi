# BDS Check

## 한 줄 요약

**Teensy R(`laserModuleR`)이 USB HID 마우스로 보낸 통과 좌표**를, 전용 씬 `BdsCheck`에서 화면 **5포인트**와 매칭해 센서·정렬 이상을 확인한다.  
Game 뷰 / 플레이어 해상도는 **1920×1080**을 기준으로 한다.

## 입력 경로 (현재 정식)

```
BB/터치 통과
  → Teensy L(θ1) + Teensy R(θ2) 삼각측량 (mm)
  → laserModuleR: Mouse.moveTo(px,py) + Mouse.click()   // SCREEN_PX = 1920×1080
  → PC USB HID 마우스
  → Unity TouchInputSource (좌클릭 좌표)
  → InputHit(screen px)
  → BdsCheckSceneController 5포인트 매칭
```

| 항목 | 값 |
|------|-----|
| Teensy 해상도 상수 | `SCREEN_PX_W=1920`, `SCREEN_PX_H=1080` |
| Unity Game 뷰 | **1920×1080** (동일해야 좌표가 맞음) |
| Unity 수신 | `TouchInputSource` ← HID 마우스 클릭 |
| 미사용(현재) | LiDAR UART → `BdsInputSource` (추후) |

에디터에서 마우스로 목표를 직접 클릭해도 같은 `TouchInputSource` 경로로 검증 가능하다.

## 왜 전용 씬인가

| 선택 | 판단 |
|------|------|
| **BdsCheck 씬 (채택)** | HID 검증 UI가 Rendezvous에 섞이지 않음 |
| Rendezvous 오버레이 | 접선 플로우와 회귀가 잦음 |

`BdsService`는 Boot DDOL을 재사용하거나, 씬 단독 Play 시 로컬 생성한다.  
미션 `IMissionController` / `MissionSessionController`는 사용하지 않는다.

## 흐름

```
Rendezvous (접선/Station)
  └─ [BDS Check] → LoadScene("BdsCheck")
                      └─ TouchInputSource로 HID hit 수신
                      └─ 5포인트 검증
                      └─ 완료 → LoadScene("Rendezvous")
```

## 현재 범위

- 5개 목표 순차 표시 → HID(또는 클릭) **실제 좌표** 표시 → 허용 반경 매칭
- 요약: **BDS(HID 정렬) 정상 / 문제 가능**
- **하지 않음:** Homography 재등록, LiDAR UART 파이프라인

## 포인트 배치 (1920×1080 · **동일 픽셀 여백**)

코너 4점은 화면 네 변에서 **같은 픽셀**만큼 떨어진다 (`cornerMarginPx`, 기본 **86px**).  
`u = px/width`, `v = px/height`로 계산한다 (정규화 동일 값 사용 금지).

| 순서 | 위치 | px (여백 86) | 정규화(참고) |
|------|------|--------------|--------------|
| 1 | 중앙 | (960, 540) | (0.50, 0.50) |
| 2 | 좌하 | (86, 86) | (0.045, 0.080) |
| 3 | 우하 | (1834, 86) | (0.955, 0.080) |
| 4 | 우상 | (1834, 994) | (0.955, 0.920) |
| 5 | 좌상 | (86, 994) | (0.045, 0.920) |

허용 반경 기본: 짧은 변의 **8%** (`matchRadiusNorm`).

Teensy `emitHid`의 Y는 `1 - yMm/H`로 뒤집어 OS/Unity 스크린 좌표(하단=0)에 맞춘다.

## HID 상태 HUD

Intro/Checking/Summary 전 구간에서 TextPanel의 **HidStatusText**가 갱신된다.

| 표시 | 의미 |
|------|------|
| `HID: <장치명>` | `Mouse.current.displayName` (없으면 `없음`) |
| `Last hit: (x, y) · Ns 전` | Teensy/`TouchInputSource`가 받은 마지막 클릭 |
| `Last hit: (대기 — inject 30 30)` | 아직 hit 없음 |
| `⚠ Screen … ≠ 1920×1080` | Game 뷰 해상도 불일치 |

**빠른 확인 (검증 시작 전)**  
1. `BdsCheck` Play (Game 뷰 1920×1080)  
2. Teensy R 시리얼: `inject 30 30`  
3. HUD `Last hit`가 ~(960, 525) 근처로 바뀌면 HID 수신 OK  

장치명만으로 Teensy와 트랙패드를 구분하지는 않는다. 목적은 **클릭·좌표 수신** 확인이다.

씬 UI를 코드로 다시 만들 때: **PinkSoft/Rebuild BdsCheck Scene** (기존 Hierarchy 수동 편집은 덮어씀).

## 관련 코드 · 씬

| 경로 | 역할 |
|------|------|
| `teensy41/laserModuleR/laserModuleR.ino` | 삼각측량 → HID `moveTo`/`click` |
| `Assets/Scenes/BdsCheck.unity` | BDS Check 전용 씬 (**uGUI** — Hierarchy에서 레이아웃 편집) |
| `Assets/Core/Runtime/BdsCheck/BdsCheckSceneController.cs` | 매칭·버튼·HID last-hit HUD |
| `Assets/Core/Editor/BdsCheckSceneBuilder.cs` | **PinkSoft/Rebuild BdsCheck Scene** |
| `Assets/BDS/Runtime/Input/TouchInputSource.cs` | HID 마우스/터치 → `InputHit` |
| `Assets/Core/Runtime/BdsService.cs` | Check 중 ActiveInput = Touch(HID) |
| `Assets/Core/Runtime/Lobby/StartMenuUI.cs` | → `LoadScene("BdsCheck")` |

UI 위치·폰트·색은 `BdsCheckCanvas`의 `TextPanel` / `ButtonBar` / `TargetMarker`를 에디터에서 직접 수정한다.


## 이후 (비범위)

- LiDAR UART 네이티브 입력으로 Check 전환
- Homography 재교정 전용 씬
- 실패 포인트만 재시도
