# Unity 프로젝트

Unity 프로젝트 루트는 **레포 루트** (`/Users/yoho/github/mi`) 입니다.

Unity Hub → **Open** → 이 레포(`mi`) 폴더를 선택하세요.

## 빠른 시작

1. **Boot** 씬 (`Assets/Scenes/Boot.unity`) 열기 → Play
2. Boot에서 Core 초기화 후 **BdsCheck** 자동 로드
3. Game 뷰 **1920×1080**. 오른쪽 SerialPanel에서 Teensy USB 연결 → `inject 30 30`으로 HID 수신 확인
4. CHECK START → 5포인트 통과

로비(Rendezvous/Station)는 동결. 하드웨어 검증이 제품이다.  
상세: [BDS Check](../../docs/bds-check.md)

에디터: Unity **6000.5 LTS** (현재 프로젝트 기준). `ProjectSettings/ProjectVersion.txt` 참고.

**렌더 파이프라인:** Universal Render Pipeline (URP) 17.5 — Built-In RP 미사용. 에셋: `Assets/Settings/URP_Pipeline.asset`.

상세 구조·씬 구성은 [루트 README](../../README.md) 및 [Mission SDK v1](../../docs/mission-sdk-v1.md)을 참고하세요.
