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
    /// <summary>Rendezvous 씬의 신원확인(접선) UI를 구성한다. Station은 별도 씬.</summary>
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

            var backdrop = CreateImage(canvasGo.transform, "Backdrop", Color.white);
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = false;
            var sceneBg = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Core/Runtime/Lobby/UI/rendezvous_scene_bg.png");
            if (sceneBg != null)
            {
                backdrop.sprite = sceneBg;
                backdrop.type = Image.Type.Simple;
                backdrop.preserveAspect = false;
                backdrop.color = Color.white;
            }
            else
            {
                backdrop.color = Bg;
            }

            // 가독성용 약한 딤 (배경 위 UI)
            var dim = CreateImage(canvasGo.transform, "BackdropDim", new Color(0.02f, 0.03f, 0.04f, 0.28f));
            Stretch(dim.rectTransform);
            dim.raycastTarget = false;

            var identity = CreateImage(canvasGo.transform, "IdentityPanel", Color.clear);
            identity.raycastTarget = false;
            Stretch(identity.rectTransform);

            // 1) 집결 코드 — 상단 중앙
            var codePanel = CreateImage(identity.transform, "RendezvousCodePanel", Color.white);
            Place(codePanel.rectTransform, 0.31f, 0.86f, 0.69f, 0.995f);
            codePanel.raycastTarget = false;
            var ribbonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Core/Runtime/Lobby/UI/rendezvous_ribbon_tape.png");
            if (ribbonSprite != null)
            {
                codePanel.sprite = ribbonSprite;
                codePanel.type = Image.Type.Simple;
                codePanel.preserveAspect = true;
                codePanel.color = Color.white;
            }
            else
            {
                codePanel.color = new Color(0.82f, 0.74f, 0.58f, 0.92f);
            }

            codePanel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2.2f);

            var stamp = CreateText(codePanel.transform, "CodeLabel", "RENDEZVOUS", 13,
                new Color(0.35f, 0.22f, 0.16f, 0.55f), FontStyle.Bold);
            Place(stamp.rectTransform, 0.08f, 0.72f, 0.92f, 0.92f);
            stamp.alignment = TextAnchor.MiddleCenter;
            stamp.font = TypewriterCodeLabel.ResolveTypewriterFont();

            var glyphRootGo = new GameObject("TypewriterGlyphs", typeof(RectTransform));
            glyphRootGo.transform.SetParent(codePanel.transform, false);
            Place(glyphRootGo.GetComponent<RectTransform>(), 0.08f, 0.18f, 0.92f, 0.78f);
            var typewriter = glyphRootGo.AddComponent<TypewriterCodeLabel>();
            var twSo = new SerializedObject(typewriter);
            twSo.FindProperty("glyphRoot").objectReferenceValue = glyphRootGo.GetComponent<RectTransform>();
            twSo.FindProperty("typewriterFont").objectReferenceValue = TypewriterCodeLabel.ResolveTypewriterFont();
            twSo.FindProperty("fontSize").intValue = 56;
            twSo.FindProperty("inkColor").colorValue = new Color(0.16f, 0.12f, 0.09f, 0.9f);
            twSo.FindProperty("letterSpacing").floatValue = 8f;
            twSo.ApplyModifiedPropertiesWithoutUndo();
            typewriter.SetCode("-----");

            var codeValue = CreateText(codePanel.transform, "CodeValue", "-----", 8,
                new Color(0, 0, 0, 0), FontStyle.Normal);
            Place(codeValue.rectTransform, 0f, 0f, 0.01f, 0.01f);

            var codePhonetic = CreateText(codePanel.transform, "CodePhonetic", "", 14,
                new Color(0.32f, 0.26f, 0.20f, 0.75f), FontStyle.Normal);
            Place(codePhonetic.rectTransform, 0.06f, 0.02f, 0.94f, 0.22f);
            codePhonetic.alignment = TextAnchor.MiddleCenter;
            codePhonetic.font = TypewriterCodeLabel.ResolveTypewriterFont();
            codePhonetic.fontSize = 15;

            // 2) 파티 — 메인 (프로스트 글래스 + 얇은 메탈 프레임 믹스)
            var partyPanel = CreateImage(identity.transform, "PartyPanel", Color.white);
            Place(partyPanel.rectTransform, 0.08f, 0.48f, 0.92f, 0.82f);
            partyPanel.raycastTarget = false;
            var partyBg = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Core/Runtime/Lobby/UI/party_panel_bg.png");
            if (partyBg != null)
            {
                partyPanel.sprite = partyBg;
                partyPanel.type = Image.Type.Simple;
                partyPanel.color = Color.white;
                partyPanel.preserveAspect = false;
            }
            else
            {
                partyPanel.color = new Color(0.08f, 0.10f, 0.12f, 0.45f);
            }

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

            // 3) 콜사인 입력 + 추가 (메탈 콘솔 스타일)
            var fieldBg = CreateImage(identity.transform, "CallsignField", Color.white);
            Place(fieldBg.rectTransform, 0.28f, 0.36f, 0.72f, 0.44f);
            ApplySprite(fieldBg, "Assets/Core/Runtime/Lobby/UI/ui_input_field.png", Color.white);
            var input = fieldBg.gameObject.AddComponent<InputField>();
            var placeholder = CreateText(fieldBg.transform, "Placeholder", "콜사인 입력", 24,
                new Color(0.65f, 0.68f, 0.72f, 0.75f), FontStyle.Italic);
            Place(placeholder.rectTransform, 0.04f, 0.15f, 0.96f, 0.85f);
            placeholder.alignment = TextAnchor.MiddleCenter;
            var inputText = CreateText(fieldBg.transform, "Text", "", 24, TextPrimary, FontStyle.Bold);
            Place(inputText.rectTransform, 0.04f, 0.15f, 0.96f, 0.85f);
            inputText.alignment = TextAnchor.MiddleCenter;
            inputText.supportRichText = false;
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.characterLimit = 24;

            var confirmBtn = CreateConfirmIdentityButton(identity.transform);
            Place(confirmBtn.GetComponent<RectTransform>(), 0.28f, 0.26f, 0.50f, 0.34f);

            // Nobody — 형체만 있는 세로 카드 슬롯 (탭으로 추가)
            var nobodyBtn = CreateNobodyCardButton(identity.transform);
            Place(nobodyBtn.GetComponent<RectTransform>(), 0.54f, 0.14f, 0.72f, 0.44f);

            var enterBtn = CreateEnterStationButton(identity.transform);
            Place(enterBtn.GetComponent<RectTransform>(), 0.26f, 0.16f, 0.52f, 0.24f);

            // 4) Clearance 안내 — 파티 뒤(아래)·가운데. 파티 생기면 숨김
            var idTitle = CreateText(identity.transform, "ClearanceTitle", "AGENT CLEARANCE", 22,
                new Color(Accent.r, Accent.g, Accent.b, 0.75f), FontStyle.Bold);
            Place(idTitle.rectTransform, 0.15f, 0.09f, 0.85f, 0.14f);
            idTitle.alignment = TextAnchor.MiddleCenter;

            var idSub = CreateText(identity.transform, "ClearanceSub",
                "콜사인 확인 또는 Nobody로 접선 · 최대 4명 · 가입은 앱에서", 16,
                new Color(TextMuted.r, TextMuted.g, TextMuted.b, 0.85f), FontStyle.Normal);
            Place(idSub.rectTransform, 0.12f, 0.04f, 0.88f, 0.09f);
            idSub.alignment = TextAnchor.MiddleCenter;

            // 상태 문구는 ClearanceSub와 역할 중복 → 짧게 가운데
            var idStatus = CreateText(identity.transform, "IdentityStatus", "", 16, TextMuted, FontStyle.Normal);
            Place(idStatus.rectTransform, 0.15f, 0.005f, 0.85f, 0.04f);
            idStatus.alignment = TextAnchor.MiddleCenter;

            var toast = CreateImage(canvasGo.transform, "StatusToast", AccentDim);
            Place(toast.rectTransform, 0.25f, 0.07f, 0.75f, 0.14f);
            var statusText = CreateText(toast.transform, "StatusText", "", 20, TextPrimary, FontStyle.Normal);
            Stretch(statusText.rectTransform);
            statusText.alignment = TextAnchor.MiddleCenter;
            toast.gameObject.SetActive(false);

            // 무전 자막
            var radioToast = CreateImage(canvasGo.transform, "RadioToast", new Color(0.12f, 0.10f, 0.08f, 0.94f));
            Place(radioToast.rectTransform, 0.12f, 0.42f, 0.88f, 0.58f);
            var radioOutline = radioToast.gameObject.AddComponent<Outline>();
            radioOutline.effectColor = new Color(0.85f, 0.55f, 0.25f, 0.7f);
            radioOutline.effectDistance = new Vector2(2f, -2f);
            var radioText = CreateText(radioToast.transform, "RadioText", "", 26, TextPrimary, FontStyle.Bold);
            Stretch(radioText.rectTransform);
            radioText.alignment = TextAnchor.MiddleCenter;
            radioToast.gameObject.SetActive(false);

            // Clearance 공통 — 우측 상단 정사각 타겟지 BDS Check (상·우 동일 마진)
            const float bdsSize = 120f;
            const float bdsMargin = 28f;
            var bdsRoot = CreateImage(canvasGo.transform, "BdsCheckHud", Color.clear);
            bdsRoot.raycastTarget = false;
            PlaceTopRightSquare(bdsRoot.rectTransform, bdsSize, bdsMargin);

            var bdsBtnGo = new GameObject("BdsCheckButton", typeof(RectTransform), typeof(Image), typeof(Button));
            bdsBtnGo.transform.SetParent(bdsRoot.transform, false);
            Stretch(bdsBtnGo.GetComponent<RectTransform>());
            var bdsImg = bdsBtnGo.GetComponent<Image>();
            bdsImg.color = Color.white;
            bdsImg.preserveAspect = true;
            var targetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Core/Runtime/Lobby/UI/bds_check_target_btn.png");
            if (targetSprite != null)
                bdsImg.sprite = targetSprite;
            else
                bdsImg.color = new Color(0.06f, 0.07f, 0.08f, 0.92f);
            var bdsBtn = bdsBtnGo.GetComponent<Button>();
            bdsBtn.targetGraphic = bdsImg;

            // 화면 하단 중앙 — 저작권처럼 연한 회사 표기
            var creditColor = new Color(0.55f, 0.58f, 0.62f, 0.22f);
            var credit = CreateText(canvasGo.transform, "CompanyCredit", "PinkSoft", 18, creditColor, FontStyle.Normal);
            Place(credit.rectTransform, 0.25f, 0.015f, 0.75f, 0.055f);
            credit.alignment = TextAnchor.MiddleCenter;

            var api = root.GetComponent<PinkSoftApiClient>() ?? root.AddComponent<PinkSoftApiClient>();
            var ui = root.GetComponent<StartMenuUI>() ?? root.AddComponent<StartMenuUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("identityPanel").objectReferenceValue = identity.gameObject;
            so.FindProperty("callsignInput").objectReferenceValue = input;
            so.FindProperty("confirmIdentityButton").objectReferenceValue = confirmBtn;
            so.FindProperty("nobodyButton").objectReferenceValue = nobodyBtn;
            so.FindProperty("enterStationButton").objectReferenceValue = enterBtn;
            so.FindProperty("identityStatusText").objectReferenceValue = idStatus;
            so.FindProperty("clearanceTitleText").objectReferenceValue = idTitle;
            so.FindProperty("clearanceSubText").objectReferenceValue = idSub;
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
            so.FindProperty("rendezvousCodeText").objectReferenceValue = codeValue;
            so.FindProperty("rendezvousPhoneticText").objectReferenceValue = codePhonetic;
            so.FindProperty("typewriterCodeLabel").objectReferenceValue = typewriter;
            so.FindProperty("statusToast").objectReferenceValue = toast.gameObject;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("bdsCheckButton").objectReferenceValue = bdsBtn;
            so.FindProperty("bdsCheckRoot").objectReferenceValue = bdsRoot.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            // BDS Check는 전용 씬 — Rendezvous 루트의 구 교정 컴포넌트/누락 스크립트 제거
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Rendezvous Start UI rebuilt (Party ≤4 → Station 씬). BDS Check → BdsCheck 씬.");
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

        static Button CreateEnterStationButton(Transform parent)
        {
            var go = new GameObject("EnterStationButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            const string spritePath = "Assets/Core/Runtime/Lobby/UI/ui_btn_enter_station.png";
            if (!ApplySprite(image, spritePath, Color.white))
                image.color = Accent;
            image.preserveAspect = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.88f, 1f);
            colors.pressedColor = new Color(0.82f, 0.72f, 0.68f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            button.colors = colors;

            var label = CreateText(go.transform, "Label", "Station 진입", 18, new Color(1f, 1f, 1f, 0f), FontStyle.Bold);
            Place(label.rectTransform, 0.1f, 0.15f, 0.9f, 0.85f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return button;
        }

        static Button CreateConfirmIdentityButton(Transform parent)
        {
            var go = new GameObject("ConfirmIdentityButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            const string spritePath = "Assets/Core/Runtime/Lobby/UI/ui_btn_confirm_identity.png";
            if (!ApplySprite(image, spritePath, Color.white))
                image.color = Accent;
            image.preserveAspect = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.92f, 0.88f, 1f);
            colors.pressedColor = new Color(0.82f, 0.72f, 0.68f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            button.colors = colors;

            // 스프라이트에 라벨/아이콘이 이미 포함됨
            var label = CreateText(go.transform, "Label", "신원 확인", 18, new Color(1f, 1f, 1f, 0f), FontStyle.Bold);
            Place(label.rectTransform, 0.1f, 0.15f, 0.9f, 0.85f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return button;
        }

        static Button CreateNobodyCardButton(Transform parent)
        {
            var go = new GameObject("NobodyCardButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            const string spritePath = "Assets/Core/Runtime/Lobby/UI/nobody_card_slot.png";
            if (!ApplySprite(image, spritePath, Color.white))
                image.color = new Color(0.12f, 0.22f, 0.30f, 0.95f);
            image.preserveAspect = true;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.70f, 0.82f, 0.90f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            button.colors = colors;

            // 접근성/폴백 라벨 — 카드 스프라이트에 NoBody가 이미 있으므로 투명
            var label = CreateText(go.transform, "Label", "NoBody", 18,
                new Color(0.85f, 0.95f, 1f, 0f), FontStyle.Bold);
            Place(label.rectTransform, 0.08f, 0.02f, 0.92f, 0.18f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return button;
        }

        static Button CreateButton(Transform parent, string label, Color face, string? spritePath = null)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 72;
            le.preferredHeight = 72;
            var image = go.GetComponent<Image>();
            if (string.IsNullOrEmpty(spritePath) || !ApplySprite(image, spritePath, Color.white))
                image.color = face;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText(go.transform, "Label", label, 24, TextPrimary, FontStyle.Bold);
            Place(text.rectTransform, 0.06f, 0.12f, 0.94f, 0.88f);
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = 24;
            return button;
        }

        static bool ApplySprite(Image image, string assetPath, Color tint)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sp == null)
                return false;
            image.sprite = sp;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = tint;
            return true;
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

        /// <summary>우상단 정사각 — 위쪽·오른쪽 마진을 동일한 픽셀로.</summary>
        static void PlaceTopRightSquare(RectTransform rt, float sizePx, float marginPx)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(sizePx, sizePx);
            rt.anchoredPosition = new Vector2(-marginPx, -marginPx);
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
