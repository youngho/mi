# Unity 프로젝트

Unity 프로젝트 루트는 **레포 루트** (`/Users/yoho/github/mi`) 입니다.

Unity Hub → **Open** → 이 레포(`mi`) 폴더를 선택하세요.

## 빠른 시작

1. **Boot** 씬 (`Assets/Scenes/Boot.unity`) 열기 → Play
2. Boot에서 Core 초기화 후 **Rendezvous** 자동 로드
3. **접선:** 콜사인 신원 확인 / **Nobody 추가** (최대 4명, Station 자동 이동 없음)
4. **Station 진입** 버튼으로 Station → 미션 목록 조회·선택  
   (우측 상단 **BDS Check** → Teensy HID 검증 씬 1920×1080 — [docs/bds-check.md](../../docs/bds-check.md))

**흐름:** 파티 등록 → Station 진입 → 미션 선택. Nobody는 계정 없는 친구용 Guest.  
상세: [Rendezvous UX](../../docs/rendezvous-flow.md)

에디터: Unity **6000.5 LTS** (현재 프로젝트 기준). `ProjectSettings/ProjectVersion.txt` 참고.

**렌더 파이프라인:** Universal Render Pipeline (URP) 17.5 — Built-In RP 미사용. 에셋: `Assets/Settings/URP_Pipeline.asset`.

상세 구조·씬 구성은 [루트 README](../../README.md) 및 [Mission SDK v1](../../docs/mission-sdk-v1.md)을 참고하세요.
