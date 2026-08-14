# mi — Agent Instructions (Antigravity)

Unity 프로젝트. 에이전트는 아래 규칙을 세션 시작부터 항상 따른다.

## Unity UI: 씬만 디자인 / C#은 기능만

UI 디자인·배치는 **씬(Hierarchy / Inspector)** 에서만 한다.
C# 스크립트는 **게임 기능·동작**만 담당한다.

### 반드시

- 위치, 크기, 앵커, 폰트, 색, 간격, 스프라이트 → **씬에서 편집**
- 버튼 클릭, API 호출, 파티/세션, 씬 전환, 상태 텍스트 갱신 → **C#**
- UI 오브젝트는 씬에 미리 두고 `[SerializeField]`로 연결한다

### 금지

- Editor 씬 빌더 / `MenuItem`으로 UI를 재생성·재배치하지 않는다 (이미 제거된 워크플로)
- 런타임에 `anchorMin` / `anchorMax` / `sizeDelta` / `fontSize` / `LayoutElement` 크기를 덮어쓰지 않는다
- 런타임에 `new GameObject`로 입력칸·패널·라벨 레이아웃을 만들지 않는다
- 전체 화면을 “예쁘게” 다시 짜는 대규모 UI 리디자인 금지 — 요청받은 최소 변경만

### 허용 (기능에 필요한 최소 UI 갱신)

- `text` / `texture` / `interactable` / `SetActive` / `enabled`
- 상태 색은 `[SerializeField] Color`로 Inspector에서 조정 가능하게 둘 수 있다
- 의도된 모션 컴포넌트(`PrimaryCtaButton`, `TypewriterCodeLabel` 등) 애니메이션은 예외

### 예시

```csharp
// ❌ BAD — 레이아웃을 코드가 소유
partyPanel.anchorMin = new Vector2(0.05f, 0.5f);
label.fontSize = 16;
var go = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage));

// ✅ GOOD — 씬 참조만 갱신
partySlotRoots[i].SetActive(occupied);
partySlotTexts[i].text = $"{slot.User.nickname}\nLv.{slot.User.currentLevel}";
identityStatusText.text = "신원 확인 중…";
```

### UI 작업 순서

1. 해당 씬을 연다 (예: `Assets/Scenes/Rendezvous.unity`)
2. Hierarchy에서 RectTransform·Text·Image를 조정한다
3. C#에는 SerializeField 바인딩과 클릭/플로우 로직만 추가·수정한다
4. Play 모드에서 레이아웃이 코드에 의해 밀리지 않는지 확인한다

## 기타

- 커밋/푸시는 사용자가 요청했을 때만 한다
- 사용자 응답은 한국어로 한다
- Cursor용 동일 규칙: `.cursor/rules/unity-ui-scene-only.mdc`
