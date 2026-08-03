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

            var idBrand = CreateText(identity.transform, "Brand", "PinkSoft", 64, TextPrimary, FontStyle.Bold);
            Place(idBrand.rectTransform, 0.08f, 0.72f, 0.7f, 0.86f);
            idBrand.alignment = TextAnchor.LowerLeft;

            var idTitle = CreateText(identity.transform, "ClearanceTitle", "AGENT CLEARANCE", 34, Accent, FontStyle.Bold);
            Place(idTitle.rectTransform, 0.08f, 0.64f, 0.7f, 0.72f);
            idTitle.alignment = TextAnchor.MiddleLeft;

            var idSub = CreateText(identity.transform, "ClearanceSub", "신원 확인 후 스테이션에 진입합니다.", 22, TextMuted, FontStyle.Normal);
            Place(idSub.rectTransform, 0.08f, 0.58f, 0.7f, 0.64f);
            idSub.alignment = TextAnchor.MiddleLeft;

            var fieldBg = CreateImage(identity.transform, "CallsignField", FieldFace);
            Place(fieldBg.rectTransform, 0.08f, 0.44f, 0.48f, 0.54f);
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
            Place(confirmBtn.GetComponent<RectTransform>(), 0.08f, 0.32f, 0.36f, 0.42f);
            Object.DestroyImmediate(confirmBtn.GetComponent<LayoutElement>());

            var idStatus = CreateText(identity.transform, "IdentityStatus", "콜사인을 입력하고 신원을 확인하세요.", 20, TextMuted, FontStyle.Normal);
            Place(idStatus.rectTransform, 0.08f, 0.24f, 0.6f, 0.31f);
            idStatus.alignment = TextAnchor.UpperLeft;

            var stationPanel = CreateImage(canvasGo.transform, "StationPanel", Color.clear);
            stationPanel.raycastTarget = false;
            Stretch(stationPanel.rectTransform);
            stationPanel.gameObject.SetActive(false);

            var content = CreateImage(stationPanel.transform, "Content", Color.clear);
            content.raycastTarget = false;
            Place(content.rectTransform, 0.08f, 0.12f, 0.55f, 0.88f);

            var brand = CreateText(content.transform, "Brand", "PinkSoft", 72, TextPrimary, FontStyle.Bold);
            Place(brand.rectTransform, 0f, 1f, 1f, 1f, new Vector2(0, -110), new Vector2(0, -20));
            brand.alignment = TextAnchor.LowerLeft;

            var title = CreateText(content.transform, "Title", "Station", 36, Accent, FontStyle.Normal);
            Place(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(0, -165), new Vector2(0, -115));
            title.alignment = TextAnchor.UpperLeft;

            var tagline = CreateText(content.transform, "Tagline", "클리어런스 승인됨. 미션을 선택하거나 센서를 설정하세요.", 22, TextMuted, FontStyle.Normal);
            Place(tagline.rectTransform, 0f, 1f, 1f, 1f, new Vector2(0, -230), new Vector2(0, -175));
            tagline.alignment = TextAnchor.UpperLeft;

            var buttons = CreateImage(content.transform, "Buttons", Color.clear);
            buttons.raycastTarget = false;
            Place(buttons.rectTransform, 0f, 0.02f, 0.85f, 0.48f);
            var layout = buttons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var missionBtn = CreateButton(buttons.transform, "미션 선택", Accent);
            var calibBtn = CreateButton(buttons.transform, "센서 설정", ButtonFace);
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
            Place(toast.rectTransform, 0.25f, 0.04f, 0.75f, 0.11f);
            var statusText = CreateText(toast.transform, "StatusText", "", 20, TextPrimary, FontStyle.Normal);
            Stretch(statusText.rectTransform);
            statusText.alignment = TextAnchor.MiddleCenter;
            toast.gameObject.SetActive(false);

            var api = root.GetComponent<PinkSoftApiClient>() ?? root.AddComponent<PinkSoftApiClient>();
            var ui = root.GetComponent<StartMenuUI>() ?? root.AddComponent<StartMenuUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("identityPanel").objectReferenceValue = identity.gameObject;
            so.FindProperty("stationPanel").objectReferenceValue = stationPanel.gameObject;
            so.FindProperty("callsignInput").objectReferenceValue = input;
            so.FindProperty("confirmIdentityButton").objectReferenceValue = confirmBtn;
            so.FindProperty("identityStatusText").objectReferenceValue = idStatus;
            so.FindProperty("apiClient").objectReferenceValue = api;
            so.FindProperty("allowOfflineClearance").boolValue = true;
            so.FindProperty("calibrationLauncher").objectReferenceValue = root.GetComponent<BdsCalibrationLauncher>();
            so.FindProperty("selectMissionButton").objectReferenceValue = missionBtn;
            so.FindProperty("calibrationButton").objectReferenceValue = calibBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("logoutButton").objectReferenceValue = logoutBtn;
            so.FindProperty("stationAgentText").objectReferenceValue = sideBody;
            so.FindProperty("statusToast").objectReferenceValue = toast.gameObject;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var legacy = root.GetComponent<LobbyCalibrationUI>();
            if (legacy != null)
                legacy.enabled = false;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Rendezvous Start UI rebuilt (Clearance → Station).");
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
    }
}
