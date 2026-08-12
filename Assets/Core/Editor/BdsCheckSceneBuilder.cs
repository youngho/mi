using PinkSoft.Core.BdsCheck;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PinkSoft.EditorTools
{
    /// <summary>
    /// BdsCheck 씬을 Canvas 오브젝트로 재구성한다.
    /// 이후 레이아웃은 Hierarchy에서 직접 편집하고, 컨트롤러는 동작만 담당한다.
    /// </summary>
    public static class BdsCheckSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/BdsCheck.unity";

        static readonly Color Bg = new(0.05f, 0.07f, 0.09f, 1f);
        static readonly Color Panel = new(0.08f, 0.10f, 0.12f, 0.88f);
        static readonly Color Accent = new(0.91f, 0.36f, 0.28f, 1f);
        static readonly Color ButtonFace = new(0.14f, 0.17f, 0.21f, 1f);
        static readonly Color TextPrimary = new(0.95f, 0.94f, 0.92f, 1f);
        static readonly Color TextMuted = new(0.72f, 0.73f, 0.74f, 1f);

        [MenuItem("PinkSoft/Rebuild BdsCheck Scene")]
        public static void Rebuild()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            var canvasGo = new GameObject("BdsCheckCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            CreateImage(canvasGo.transform, "Backdrop", new Color(0.05f, 0.07f, 0.09f, 0.92f), stretch: true);

            var target = CreateTargetMarker(canvasGo.transform);
            var (hitRts, hitImgs) = CreateHitMarkers(canvasGo.transform);

            var textPanel = CreateImage(canvasGo.transform, "TextPanel", Panel, stretch: false);
            Place(textPanel.rectTransform, 0.14f, 0.20f, 0.62f, 0.93f);
            var title = CreateText(textPanel.transform, "TitleText", "BDS Check — Teensy R HID", 40, TextPrimary, FontStyle.Bold);
            Place(title.rectTransform, 0.04f, 0.84f, 0.96f, 0.98f);
            var status = CreateText(textPanel.transform, "StatusText", "status", 22, TextMuted, FontStyle.Normal);
            Place(status.rectTransform, 0.04f, 0.68f, 0.96f, 0.84f);
            var hidStatus = CreateText(textPanel.transform, "HidStatusText", "HID: …", 22, TextMuted, FontStyle.Normal);
            Place(hidStatus.rectTransform, 0.04f, 0.52f, 0.96f, 0.68f);
            var body = CreateText(textPanel.transform, "BodyText", "body", 26, TextPrimary, FontStyle.Normal);
            Place(body.rectTransform, 0.04f, 0.04f, 0.96f, 0.52f);
            body.alignment = TextAnchor.UpperCenter;

            var serial = CreateSerialPanel(canvasGo.transform);

            var buttonBar = CreateImage(canvasGo.transform, "ButtonBar", Panel, stretch: false);
            Place(buttonBar.rectTransform, 0.14f, 0.02f, 0.62f, 0.16f);

            var introGroup = new GameObject("IntroButtons", typeof(RectTransform));
            introGroup.transform.SetParent(buttonBar.transform, false);
            Stretch(introGroup.GetComponent<RectTransform>());
            var startBtn = CreateButton(introGroup.transform, "검증 시작", Accent);
            Place(startBtn.GetComponent<RectTransform>(), 0.05f, 0.52f, 0.95f, 0.92f);
            var introBack = CreateButton(introGroup.transform, "돌아가기", ButtonFace);
            Place(introBack.GetComponent<RectTransform>(), 0.05f, 0.08f, 0.95f, 0.46f);

            var checkingGroup = new GameObject("CheckingButtons", typeof(RectTransform));
            checkingGroup.transform.SetParent(buttonBar.transform, false);
            Stretch(checkingGroup.GetComponent<RectTransform>());
            checkingGroup.SetActive(false);
            var skipBtn = CreateButton(checkingGroup.transform, "이 포인트 건너뛰기", Accent);
            Place(skipBtn.GetComponent<RectTransform>(), 0.05f, 0.52f, 0.95f, 0.92f);
            var restartBtn = CreateButton(checkingGroup.transform, "처음부터", ButtonFace);
            Place(restartBtn.GetComponent<RectTransform>(), 0.05f, 0.08f, 0.48f, 0.46f);
            var abortBtn = CreateButton(checkingGroup.transform, "중단", ButtonFace);
            Place(abortBtn.GetComponent<RectTransform>(), 0.52f, 0.08f, 0.95f, 0.46f);

            var summaryGroup = new GameObject("SummaryButtons", typeof(RectTransform));
            summaryGroup.transform.SetParent(buttonBar.transform, false);
            Stretch(summaryGroup.GetComponent<RectTransform>());
            summaryGroup.SetActive(false);
            var retryBtn = CreateButton(summaryGroup.transform, "다시 검증", ButtonFace);
            Place(retryBtn.GetComponent<RectTransform>(), 0.05f, 0.52f, 0.95f, 0.92f);
            var doneBtn = CreateButton(summaryGroup.transform, "완료 — Rendezvous로", Accent);
            Place(doneBtn.GetComponent<RectTransform>(), 0.05f, 0.08f, 0.95f, 0.46f);

            var root = new GameObject("PMS_BdsCheck");
            var controller = root.AddComponent<BdsCheckSceneController>();
            var so = new SerializedObject(controller);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("statusText").objectReferenceValue = status;
            so.FindProperty("hidStatusText").objectReferenceValue = hidStatus;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("introButtonGroup").objectReferenceValue = introGroup;
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.FindProperty("introBackButton").objectReferenceValue = introBack;
            so.FindProperty("checkingButtonGroup").objectReferenceValue = checkingGroup;
            so.FindProperty("skipButton").objectReferenceValue = skipBtn;
            so.FindProperty("restartButton").objectReferenceValue = restartBtn;
            so.FindProperty("abortButton").objectReferenceValue = abortBtn;
            so.FindProperty("summaryButtonGroup").objectReferenceValue = summaryGroup;
            so.FindProperty("retryButton").objectReferenceValue = retryBtn;
            so.FindProperty("doneButton").objectReferenceValue = doneBtn;
            so.FindProperty("targetMarker").objectReferenceValue = target;
            so.FindProperty("targetMarkerLabel").objectReferenceValue =
                target.Find("Label")?.GetComponent<Text>();

            var hitsProp = so.FindProperty("hitMarkers");
            hitsProp.arraySize = hitRts.Length;
            var hitImgProp = so.FindProperty("hitMarkerImages");
            hitImgProp.arraySize = hitImgs.Length;
            for (var i = 0; i < hitRts.Length; i++)
            {
                hitsProp.GetArrayElementAtIndex(i).objectReferenceValue = hitRts[i];
                hitImgProp.GetArrayElementAtIndex(i).objectReferenceValue = hitImgs[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            var monitor = root.AddComponent<TeensySerialMonitor>();
            var mso = new SerializedObject(monitor);
            mso.FindProperty("portDropdown").objectReferenceValue = serial.PortDropdown;
            mso.FindProperty("refreshButton").objectReferenceValue = serial.RefreshButton;
            mso.FindProperty("connectButton").objectReferenceValue = serial.ConnectButton;
            mso.FindProperty("connectButtonLabel").objectReferenceValue =
                serial.ConnectButton.transform.Find("Label")?.GetComponent<Text>();
            mso.FindProperty("statusLabel").objectReferenceValue = serial.StatusLabel;
            mso.FindProperty("logText").objectReferenceValue = serial.LogText;
            mso.FindProperty("logScroll").objectReferenceValue = serial.LogScroll;
            mso.FindProperty("commandInput").objectReferenceValue = serial.CommandInput;
            mso.FindProperty("sendButton").objectReferenceValue = serial.SendButton;
            mso.FindProperty("injectButton").objectReferenceValue = serial.InjectButton;
            mso.FindProperty("statusCmdButton").objectReferenceValue = serial.StatusCmdButton;
            mso.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EnsureInBuildSettings();
            Debug.Log(
                $"BdsCheck uGUI scene rebuilt: {ScenePath} — Hierarchy에서 TextPanel/ButtonBar/SerialPanel을 편집하세요. " +
                "(Rebuild는 씬을 덮어씁니다.)");
        }

        struct SerialPanelRefs
        {
            public Dropdown PortDropdown;
            public Button RefreshButton;
            public Button ConnectButton;
            public Text StatusLabel;
            public Text LogText;
            public ScrollRect LogScroll;
            public InputField CommandInput;
            public Button SendButton;
            public Button InjectButton;
            public Button StatusCmdButton;
        }

        static SerialPanelRefs CreateSerialPanel(Transform canvas)
        {
            var panel = CreateImage(canvas, "SerialPanel", Panel, stretch: false);
            panel.raycastTarget = true;
            Place(panel.rectTransform, 0.64f, 0.02f, 0.98f, 0.93f);

            var heading = CreateText(panel.transform, "SerialTitle", "Teensy USB Serial", 26, TextPrimary, FontStyle.Bold);
            Place(heading.rectTransform, 0.04f, 0.94f, 0.96f, 0.99f);
            heading.alignment = TextAnchor.MiddleLeft;

            var baud = CreateText(panel.transform, "BaudLabel", "115200 8N1", 16, TextMuted, FontStyle.Normal);
            Place(baud.rectTransform, 0.04f, 0.90f, 0.96f, 0.94f);
            baud.alignment = TextAnchor.MiddleLeft;

            var dropdown = CreateDropdown(panel.transform, "PortDropdown");
            Place(dropdown.GetComponent<RectTransform>(), 0.04f, 0.83f, 0.96f, 0.89f);

            var refreshBtn = CreateButton(panel.transform, "Refresh", ButtonFace);
            Place(refreshBtn.GetComponent<RectTransform>(), 0.04f, 0.76f, 0.48f, 0.82f);
            SetButtonLabelSize(refreshBtn, 20);

            var connectBtn = CreateButton(panel.transform, "연결", Accent);
            Place(connectBtn.GetComponent<RectTransform>(), 0.52f, 0.76f, 0.96f, 0.82f);
            SetButtonLabelSize(connectBtn, 20);

            var status = CreateText(panel.transform, "SerialStatus", "대기", 16, TextMuted, FontStyle.Normal);
            Place(status.rectTransform, 0.04f, 0.71f, 0.96f, 0.76f);
            status.alignment = TextAnchor.MiddleLeft;

            var (scroll, logText) = CreateLogScroll(panel.transform);
            Place(scroll.GetComponent<RectTransform>(), 0.04f, 0.18f, 0.96f, 0.70f);

            var cmdBg = CreateImage(panel.transform, "CommandField", new Color(0.05f, 0.06f, 0.08f, 1f), stretch: false);
            cmdBg.raycastTarget = true;
            Place(cmdBg.rectTransform, 0.04f, 0.10f, 0.72f, 0.16f);
            var input = cmdBg.gameObject.AddComponent<InputField>();
            var placeholder = CreateText(cmdBg.transform, "Placeholder", "명령 (예: status)", 18, new Color(0.5f, 0.52f, 0.55f, 1f), FontStyle.Normal);
            Stretch(placeholder.rectTransform);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.raycastTarget = false;
            var inputText = CreateText(cmdBg.transform, "Text", "", 18, TextPrimary, FontStyle.Normal);
            Stretch(inputText.rectTransform);
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.supportRichText = false;
            input.textComponent = inputText;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;

            var sendBtn = CreateButton(panel.transform, "전송", Accent);
            Place(sendBtn.GetComponent<RectTransform>(), 0.74f, 0.10f, 0.96f, 0.16f);
            SetButtonLabelSize(sendBtn, 18);

            var injectBtn = CreateButton(panel.transform, "inject 30 30", ButtonFace);
            Place(injectBtn.GetComponent<RectTransform>(), 0.04f, 0.02f, 0.48f, 0.08f);
            SetButtonLabelSize(injectBtn, 16);

            var statusCmdBtn = CreateButton(panel.transform, "status", ButtonFace);
            Place(statusCmdBtn.GetComponent<RectTransform>(), 0.52f, 0.02f, 0.96f, 0.08f);
            SetButtonLabelSize(statusCmdBtn, 16);

            return new SerialPanelRefs
            {
                PortDropdown = dropdown,
                RefreshButton = refreshBtn,
                ConnectButton = connectBtn,
                StatusLabel = status,
                LogText = logText,
                LogScroll = scroll,
                CommandInput = input,
                SendButton = sendBtn,
                InjectButton = injectBtn,
                StatusCmdButton = statusCmdBtn
            };
        }

        static void SetButtonLabelSize(Button button, int size)
        {
            var label = button.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
                label.fontSize = size;
        }

        static (ScrollRect scroll, Text logText) CreateLogScroll(Transform parent)
        {
            var scrollGo = new GameObject("SerialLogScroll", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0.04f, 0.05f, 0.06f, 1f);
            scrollImg.raycastTarget = true;
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = false;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollGo.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Image>().raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var log = CreateText(content.transform, "LogText", "", 16, new Color(0.78f, 0.86f, 0.72f, 1f), FontStyle.Normal);
            var logRt = log.rectTransform;
            logRt.anchorMin = new Vector2(0f, 1f);
            logRt.anchorMax = new Vector2(1f, 1f);
            logRt.pivot = new Vector2(0.5f, 1f);
            logRt.anchoredPosition = Vector2.zero;
            logRt.sizeDelta = new Vector2(0f, 40f);
            log.alignment = TextAnchor.UpperLeft;
            log.horizontalOverflow = HorizontalWrapMode.Wrap;
            log.verticalOverflow = VerticalWrapMode.Overflow;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;
            return (scroll, log);
        }

        static Dropdown CreateDropdown(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Dropdown));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = new Color(0.10f, 0.12f, 0.15f, 1f);
            image.raycastTarget = true;
            var dropdown = go.GetComponent<Dropdown>();

            var label = CreateText(go.transform, "Label", "포트 선택", 18, TextPrimary, FontStyle.Normal);
            Place(label.rectTransform, 0.04f, 0.05f, 0.88f, 0.95f);
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;

            var arrow = CreateText(go.transform, "Arrow", "▾", 18, TextMuted, FontStyle.Normal);
            Place(arrow.rectTransform, 0.88f, 0.05f, 0.98f, 0.95f);
            arrow.alignment = TextAnchor.MiddleCenter;

            var template = new GameObject("Template", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            template.transform.SetParent(go.transform, false);
            var templateRt = template.GetComponent<RectTransform>();
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.sizeDelta = new Vector2(0f, 160f);
            templateRt.anchoredPosition = Vector2.zero;
            template.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 1f);
            var templateScroll = template.GetComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.vertical = true;
            template.SetActive(false);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 36f);

            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            Stretch(item.GetComponent<RectTransform>());
            var itemBg = item.AddComponent<Image>();
            itemBg.color = new Color(0.12f, 0.14f, 0.17f, 1f);
            var toggle = item.GetComponent<Toggle>();
            toggle.targetGraphic = itemBg;

            var itemLabel = CreateText(item.transform, "Item Label", "Option", 18, TextPrimary, FontStyle.Normal);
            Stretch(itemLabel.rectTransform);
            itemLabel.alignment = TextAnchor.MiddleLeft;

            templateScroll.viewport = viewport.GetComponent<RectTransform>();
            templateScroll.content = contentRt;
            dropdown.template = templateRt;
            dropdown.captionText = label;
            dropdown.itemText = itemLabel;
            dropdown.options = new System.Collections.Generic.List<Dropdown.OptionData>();
            return dropdown;
        }

        static RectTransform CreateTargetMarker(Transform parent)
        {
            var root = new GameObject("TargetMarker", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120, 120);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var h = CreateImage(root.transform, "CrossH", Accent, stretch: false);
            Place(h.rectTransform, 0.1f, 0.46f, 0.9f, 0.54f);
            var v = CreateImage(root.transform, "CrossV", Accent, stretch: false);
            Place(v.rectTransform, 0.46f, 0.1f, 0.54f, 0.9f);
            var ring = CreateImage(root.transform, "Ring", new Color(1f, 1f, 1f, 0.35f), stretch: false);
            Place(ring.rectTransform, 0.05f, 0.05f, 0.95f, 0.95f);
            ring.color = new Color(1f, 1f, 1f, 0.25f);

            var label = CreateText(root.transform, "Label", "1/5", 22, TextPrimary, FontStyle.Bold);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0f);
            lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 1f);
            lrt.anchoredPosition = new Vector2(0f, -10f);
            lrt.sizeDelta = new Vector2(260f, 40f);

            root.SetActive(false);
            return rt;
        }

        static (RectTransform[] rts, Image[] imgs) CreateHitMarkers(Transform parent)
        {
            var root = new GameObject("HitMarkers", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Stretch(root.GetComponent<RectTransform>());

            var rts = new RectTransform[5];
            var imgs = new Image[5];
            for (var i = 0; i < 5; i++)
            {
                var img = CreateImage(root.transform, $"Hit{i}", Color.green, stretch: false);
                var rt = img.rectTransform;
                rt.sizeDelta = new Vector2(28, 28);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                img.gameObject.SetActive(false);
                rts[i] = rt;
                imgs[i] = img;
            }

            return (rts, imgs);
        }

        static Image CreateImage(Transform parent, string name, Color color, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (stretch)
                Stretch(image.rectTransform);
            return image;
        }

        static Text CreateText(Transform parent, string name, string content, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button CreateButton(Transform parent, string label, Color face)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = face;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText(go.transform, "Label", label, 28, TextPrimary, FontStyle.Bold);
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
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void EnsureInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == ScenePath)
                    return;
            }

            var list = new EditorBuildSettingsScene[scenes.Length + 1];
            for (var i = 0; i < scenes.Length; i++)
                list[i] = scenes[i];
            list[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = list;
        }
    }
}
