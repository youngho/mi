using PinkSoft.Core;
using PinkSoft.Core.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PinkSoft.EditorTools
{
    /// <summary>Rendezvous 씬의 신원확인/Station UI를 씬 오브젝트로 구성한다.</summary>
    public static class StartMenuSceneBuilder
    {
        static readonly Color Bg = new(0.05f, 0.07f, 0.09f, 1f);
        static readonly Color Panel = new(0.09f, 0.12f, 0.15f, 0.92f);
        static readonly Color Accent = new(0.91f, 0.36f, 0.28f, 1f);
        static readonly Color AccentDim = new(0.55f, 0.22f, 0.18f, 1f);
        static readonly Color TextPrimary = new(0.95f, 0.94f, 0.92f, 1f);
        static readonly Color TextMuted = new(0.55f, 0.58f, 0.62f, 1f);
        static readonly Color ButtonFace = new(0.12f, 0.16f, 0.20f, 1f);
        static readonly Color FieldFace = new(0.08f, 0.10f, 0.13f, 1f);

        [MenuItem("PinkSoft/Rebuild Rendezvous Start UI")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Rendezvous.unity");
            var root = GameObject.Find("PMS_Rendezvous") ?? GameObject.Find("PMS_Lobby");
            if (root == null)
            {
                Debug.LogError("PMS_Rendezvous not found");
                return;
            }

            root.name = "PMS_Rendezvous";
            DestroyIfExists("StartMenuCanvas");
            DestroyIfExists("EventSystem");

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Bg;
                EditorUtility.SetDirty(cam);
            }

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("StartMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var backdrop = CreateImage(canvasGo.transform, "Backdrop", Bg);
            Stretch(backdrop.rectTransform);

            var identity = CreateImage(canvasGo.transform, "IdentityPanel", Color.clear);
            identity.raycastTarget = false;
            Stretch(identity.rectTransform);

            var idTitle = CreateText(identity.transform, "ClearanceTitle", "AGENT CLEARANCE", 34, Accent, FontStyle.Bold);
            Place(idTitle.rectTransform, 0.08f, 0.88f, 0.55f, 0.96f);
            idTitle.alignment = TextAnchor.MiddleLeft;

            var idSub = CreateText(identity.transform, "ClearanceSub", "최대 4명까지 접선 후 Station에 진입합니다. 회원가입은 앱에서 해주세요.", 20, TextMuted, FontStyle.Normal);
            Place(idSub.rectTransform, 0.08f, 0.82f, 0.55f, 0.88f);
            idSub.alignment = TextAnchor.MiddleLeft;

            var fieldBg = CreateImage(identity.transform, "CallsignField", FieldFace);
            Place(fieldBg.rectTransform, 0.31f, 0.40f, 0.65f, 0.48f);
            var input = fieldBg.gameObject.AddComponent<InputField>();
            var placeholder = CreateText(fieldBg.transform, "Placeholder", "콜사인 입력", 26, TextMuted, FontStyle.Italic);
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAnchor.MiddleLeft;
            var inputText = CreateText(fieldBg.transform, "Text", "", 26, TextPrimary, FontStyle.Normal);
            Stretch(inputText.rectTransform);
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.supportRichText = false;
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.characterLimit = 24;

            var confirmBtn = CreateButton(identity.transform, "신원 확인", Accent);
            Place(confirmBtn.GetComponent<RectTransform>(), 0.31f, 0.30f, 0.51f, 0.38f);
            Object.DestroyImmediate(confirmBtn.GetComponent<LayoutElement>());

            var nobodyBtn = CreateButton(identity.transform, "Nobody 추가", ButtonFace);
            Place(nobodyBtn.GetComponent<RectTransform>(), 0.53f, 0.30f, 0.73f, 0.38f);
            Object.DestroyImmediate(nobodyBtn.GetComponent<LayoutElement>());

            // 화면 중앙 — 초상 카드용으로 세로 대역 확대
            var partyYMin = 2f / 3f - 0.16f;
            var partyYMax = 2f / 3f + 0.14f;
            var partyPanel = CreateImage(identity.transform, "PartyPanel", new Color(0.08f, 0.10f, 0.12f, 0.22f));
            Place(partyPanel.rectTransform, 0.05f, partyYMin, 0.95f, partyYMax);
            var partyOutline = partyPanel.gameObject.AddComponent<Outline>();
            partyOutline.effectColor = new Color(0.78f, 0.82f, 0.86f, 0.40f);
            partyOutline.effectDistance = new Vector2(2f, -2f);
            partyOutline.useGraphicAlpha = true;

            var slotsRoot = CreateImage(partyPanel.transform, "PartySlots", Color.clear);
            slotsRoot.raycastTarget = false;
            Place(slotsRoot.rectTransform, 0.02f, 0.08f, 0.98f, 0.92f);
            var slotsLayout = slotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            slotsLayout.spacing = 16;
            slotsLayout.padding = new RectOffset(12, 12, 6, 6);
            slotsLayout.childAlignment = TextAnchor.MiddleCenter;
            slotsLayout.childControlHeight = true;
            slotsLayout.childControlWidth = true;
            slotsLayout.childForceExpandHeight = true;
            slotsLayout.childForceExpandWidth = false;

            var slotTexts = new Text[4];
            var slotBgs = new Image[4];
            var slotRoots = new GameObject[4];
            var slotPortraits = new RawImage[4];
            var chipFace = new Color(0.16f, 0.22f, 0.28f, 0.85f);
            for (var i = 0; i < 4; i++)
            {
                var slotGo = new GameObject($"PartySlot{i + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                slotGo.transform.SetParent(slotsRoot.transform, false);
                var le = slotGo.GetComponent<LayoutElement>();
                le.minWidth = 140;
                le.preferredWidth = 168;
                le.minHeight = 200;
                le.preferredHeight = 220;
                var bg = slotGo.GetComponent<Image>();
                bg.color = chipFace;
                slotBgs[i] = bg;
                slotRoots[i] = slotGo;
                slotGo.SetActive(false);

                var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                portraitGo.transform.SetParent(slotGo.transform, false);
                var portraitRt = portraitGo.GetComponent<RectTransform>();
                portraitRt.anchorMin = new Vector2(0.08f, 0.28f);
                portraitRt.anchorMax = new Vector2(0.92f, 0.94f);
                portraitRt.offsetMin = Vector2.zero;
                portraitRt.offsetMax = Vector2.zero;
                var portrait = portraitGo.GetComponent<RawImage>();
                portrait.raycastTarget = false;
                portrait.color = Color.white;
                slotPortraits[i] = portrait;

                var label = CreateText(slotGo.transform, "Label", "", 16, TextPrimary, FontStyle.Bold);
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0.04f, 0.02f);
                labelRt.anchorMax = new Vector2(0.96f, 0.28f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                label.alignment = TextAnchor.MiddleCenter;
                slotTexts[i] = label;
            }

            var enterBtn = CreateButton(identity.transform, "Station 진입", Accent);
            Place(enterBtn.GetComponent<RectTransform>(), 0.08f, 0.22f, 0.36f, 0.34f);
            Object.DestroyImmediate(enterBtn.GetComponent<LayoutElement>());

            var idStatus = CreateText(identity.transform, "IdentityStatus", "기존 콜사인으로 신원 확인하거나 Nobody를 추가하세요. 회원가입은 앱에서 해주세요.", 18, TextMuted, FontStyle.Normal);
            Place(idStatus.rectTransform, 0.08f, 0.12f, 0.55f, 0.20f);
            idStatus.alignment = TextAnchor.UpperLeft;

            var stationPanel = CreateImage(canvasGo.transform, "StationPanel", Color.clear);
            stationPanel.raycastTarget = false;
            Stretch(stationPanel.rectTransform);
            stationPanel.gameObject.SetActive(false);

            var content = CreateImage(stationPanel.transform, "Content", Color.clear);
            content.raycastTarget = false;
            Place(content.rectTransform, 0.08f, 0.12f, 0.55f, 0.88f);

            var title = CreateText(content.transform, "Title", "Station", 42, Accent, FontStyle.Bold);
            Place(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(0, -90), new Vector2(0, -20));
            title.alignment = TextAnchor.LowerLeft;

            var tagline = CreateText(content.transform, "Tagline", "클리어런스 승인됨. 미션을 선택하세요.", 22, TextMuted, FontStyle.Normal);
            Place(tagline.rectTransform, 0f, 1f, 1f, 1f, new Vector2(0, -150), new Vector2(0, -100));
            tagline.alignment = TextAnchor.UpperLeft;

            var buttons = CreateImage(content.transform, "Buttons", Color.clear);
            buttons.raycastTarget = false;
            Place(buttons.rectTransform, 0f, 0.02f, 0.85f, 0.55f);
            var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var missionBtn = CreateButton(buttons.transform, "미션 선택", Accent);
            var logoutBtn = CreateButton(buttons.transform, "클리어런스 해제", ButtonFace);
            var quitBtn = CreateButton(buttons.transform, "종료", ButtonFace);

            var side = CreateImage(stationPanel.transform, "SidePanel", Panel);
            Place(side.rectTransform, 0.62f, 0.18f, 0.92f, 0.82f);
            var sideTitle = CreateText(side.transform, "SideTitle", "스테이션", 28, TextPrimary, FontStyle.Bold);
            Place(sideTitle.rectTransform, 0.08f, 0.82f, 0.92f, 0.95f);
            sideTitle.alignment = TextAnchor.MiddleLeft;
            var sideBody = CreateText(side.transform, "StationAgentText", "", 20, TextMuted, FontStyle.Normal);
            Place(sideBody.rectTransform, 0.08f, 0.12f, 0.92f, 0.78f);
            sideBody.alignment = TextAnchor.UpperLeft;

            var toast = CreateImage(canvasGo.transform, "StatusToast", AccentDim);
            Place(toast.rectTransform, 0.25f, 0.07f, 0.75f, 0.14f);
            var statusText = CreateText(toast.transform, "StatusText", "", 20, TextPrimary, FontStyle.Normal);
            Stretch(statusText.rectTransform);
            statusText.alignment = TextAnchor.MiddleCenter;
            toast.gameObject.SetActive(false);

            // Clearance·Station 공통 — 우측 상단 BDS Check 특수 버튼
            var bdsRoot = CreateImage(canvasGo.transform, "BdsCheckHud", Color.clear);
            bdsRoot.raycastTarget = false;
            Place(bdsRoot.rectTransform, 0.78f, 0.86f, 0.98f, 0.98f);

            var bdsFace = new Color(0.10f, 0.14f, 0.17f, 0.95f);
            var bdsBtnGo = new GameObject("BdsCheckButton", typeof(RectTransform), typeof(Image), typeof(Button));
            bdsBtnGo.transform.SetParent(bdsRoot.transform, false);
            Stretch(bdsBtnGo.GetComponent<RectTransform>());
            var bdsImg = bdsBtnGo.GetComponent<Image>();
            bdsImg.color = bdsFace;
            var bdsBtn = bdsBtnGo.GetComponent<Button>();
            bdsBtn.targetGraphic = bdsImg;

            var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Core/Runtime/Lobby/UI/bds_check_icon.png");
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(bdsBtnGo.transform, false);
            Place(iconGo.GetComponent<RectTransform>(), 0.06f, 0.18f, 0.34f, 0.82f);
            var iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;
            if (iconSprite != null)
            {
                iconImg.sprite = iconSprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
            }
            else
            {
                iconImg.color = Accent;
            }

            var bdsLabel = CreateText(bdsBtnGo.transform, "Label", "BDS Check", 20, TextPrimary, FontStyle.Bold);
            Place(bdsLabel.rectTransform, 0.36f, 0.15f, 0.96f, 0.85f);
            bdsLabel.alignment = TextAnchor.MiddleLeft;

            // 화면 하단 중앙 — 저작권처럼 연한 회사 표기
            var creditColor = new Color(0.55f, 0.58f, 0.62f, 0.22f);
            var credit = CreateText(canvasGo.transform, "CompanyCredit", "PinkSoft", 18, creditColor, FontStyle.Normal);
            Place(credit.rectTransform, 0.25f, 0.015f, 0.75f, 0.055f);
            credit.alignment = TextAnchor.MiddleCenter;

            var api = root.GetComponent<PinkSoftApiClient>() ?? root.AddComponent<PinkSoftApiClient>();
            var ui = root.GetComponent<StartMenuUI>() ?? root.AddComponent<StartMenuUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("identityPanel").objectReferenceValue = identity.gameObject;
            so.FindProperty("stationPanel").objectReferenceValue = stationPanel.gameObject;
            so.FindProperty("callsignInput").objectReferenceValue = input;
            so.FindProperty("confirmIdentityButton").objectReferenceValue = confirmBtn;
            so.FindProperty("nobodyButton").objectReferenceValue = nobodyBtn;
            so.FindProperty("enterStationButton").objectReferenceValue = enterBtn;
            so.FindProperty("identityStatusText").objectReferenceValue = idStatus;
            var slotTextsProp = so.FindProperty("partySlotTexts");
            slotTextsProp.arraySize = 4;
            var slotBgsProp = so.FindProperty("partySlotBackgrounds");
            slotBgsProp.arraySize = 4;
            var slotRootsProp = so.FindProperty("partySlotRoots");
            slotRootsProp.arraySize = 4;
            var slotPortraitsProp = so.FindProperty("partySlotPortraits");
            slotPortraitsProp.arraySize = 4;
            for (var i = 0; i < 4; i++)
            {
                slotTextsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotTexts[i];
                slotBgsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotBgs[i];
                slotRootsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotRoots[i];
                slotPortraitsProp.GetArrayElementAtIndex(i).objectReferenceValue = slotPortraits[i];
            }
            so.FindProperty("apiClient").objectReferenceValue = api;
            var nobodyPortrait = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/PartyPortrait/NobodyPortrait.png")
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/PartyPortrait/NobodyPortrait.png");
            if (nobodyPortrait != null)
                so.FindProperty("nobodyPortraitTexture").objectReferenceValue = nobodyPortrait;
            so.FindProperty("allowOfflineClearance").boolValue = true;
            so.FindProperty("selectMissionButton").objectReferenceValue = missionBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("logoutButton").objectReferenceValue = logoutBtn;
            so.FindProperty("stationAgentText").objectReferenceValue = sideBody;
            so.FindProperty("statusToast").objectReferenceValue = toast.gameObject;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("bdsCheckButton").objectReferenceValue = bdsBtn;
            so.FindProperty("bdsCheckRoot").objectReferenceValue = bdsRoot.gameObject;
            so.FindProperty("bdsCheckSceneName").stringValue = "BdsCheck";
            so.ApplyModifiedPropertiesWithoutUndo();

            // BDS Check는 전용 씬 — Rendezvous 루트의 구 교정 컴포넌트/누락 스크립트 제거
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Rendezvous Start UI rebuilt (Party ≤4 → Station 진입). BDS Check → BdsCheck 씬.");
        }

        static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
                Object.DestroyImmediate(go);
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(Transform parent, string name, string value, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button CreateButton(Transform parent, string label, Color face)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 72;
            le.preferredHeight = 72;
            var image = go.GetComponent<Image>();
            image.color = face;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText(go.transform, "Label", label, 26, TextPrimary, FontStyle.Bold);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            Place(rt, xMin, yMin, xMax, yMax, Vector2.zero, Vector2.zero);
        }

        static void Place(RectTransform rt, float xMin, float yMin, float xMax, float yMax, Vector2 minOffset, Vector2 maxOffset)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = minOffset;
            rt.offsetMax = maxOffset;
        }

        [MenuItem("PinkSoft/Bake Selected UI Anchors")]
        public static void BakeSelectedAnchors()
        {
            var targets = Selection.transforms;
            if (targets == null || targets.Length == 0)
            {
                Debug.LogWarning("Bake Selected UI Anchors: RectTransform을 선택하세요.");
                return;
            }

            var list = new System.Collections.Generic.List<RectTransform>();
            foreach (var t in targets)
            {
                if (t is RectTransform rt && rt.parent is RectTransform)
                    list.Add(rt);
            }

            list.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
            var n = 0;
            foreach (var rt in list)
            {
                if (BakeAnchorsToCurrentRect(rt))
                    n++;
            }

            if (n > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Bake Selected UI Anchors: {n}개 정리");
        }

        [MenuItem("PinkSoft/Bake StartMenuCanvas Anchors")]
        public static void BakeStartMenuCanvasAnchors()
        {
            var canvas = GameObject.Find("StartMenuCanvas");
            if (canvas == null)
            {
                Debug.LogError("StartMenuCanvas not found");
                return;
            }

            var list = new System.Collections.Generic.List<RectTransform>();
            foreach (var rt in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.parent is RectTransform)
                    list.Add(rt);
            }

            list.Sort((a, b) => Depth(a).CompareTo(Depth(b)));
            var n = 0;
            foreach (var rt in list)
            {
                if (BakeAnchorsToCurrentRect(rt))
                    n++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"Bake StartMenuCanvas Anchors: {n}개 변경 / 전체 {list.Count}");
        }

        static int Depth(Transform t)
        {
            var d = 0;
            for (var p = t; p != null; p = p.parent)
                d++;
            return d;
        }

        static bool BakeAnchorsToCurrentRect(RectTransform rt)
        {
            var parent = rt.parent as RectTransform;
            if (parent == null)
                return false;

            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            for (var i = 0; i < 4; i++)
                corners[i] = parent.InverseTransformPoint(corners[i]);

            var pr = parent.rect;
            var w = Mathf.Max(pr.width, 0.0001f);
            var h = Mathf.Max(pr.height, 0.0001f);
            var amin = new Vector2(
                Mathf.Round(((corners[0].x - pr.xMin) / w) * 10000f) / 10000f,
                Mathf.Round(((corners[0].y - pr.yMin) / h) * 10000f) / 10000f);
            var amax = new Vector2(
                Mathf.Round(((corners[2].x - pr.xMin) / w) * 10000f) / 10000f,
                Mathf.Round(((corners[2].y - pr.yMin) / h) * 10000f) / 10000f);

            var changed = (amin - rt.anchorMin).sqrMagnitude > 1e-8f
                          || (amax - rt.anchorMax).sqrMagnitude > 1e-8f
                          || rt.anchoredPosition.sqrMagnitude > 1e-6f
                          || rt.sizeDelta.sqrMagnitude > 1e-6f;
            if (!changed)
                return false;

            Undo.RecordObject(rt, "Bake UI Anchors");
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            EditorUtility.SetDirty(rt);
            return true;
        }
    }
}
