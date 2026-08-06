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
    /// <summary>Station 씬을 구성한다. 에이전트 정보는 AgentSession(DDOL)에서 읽는다.</summary>
    public static class StationSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Station.unity";

        static readonly Color Bg = new(0.05f, 0.07f, 0.09f, 1f);
        static readonly Color Panel = new(0.09f, 0.12f, 0.15f, 0.92f);
        static readonly Color Accent = new(0.91f, 0.36f, 0.28f, 1f);
        static readonly Color AccentDim = new(0.55f, 0.22f, 0.18f, 1f);
        static readonly Color TextPrimary = new(0.95f, 0.94f, 0.92f, 1f);
        static readonly Color TextMuted = new(0.55f, 0.58f, 0.62f, 1f);
        static readonly Color ButtonFace = new(0.12f, 0.16f, 0.20f, 1f);

        [MenuItem("PinkSoft/Rebuild Station Scene")]
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

            var canvasGo = new GameObject("StationCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            CreateImage(canvasGo.transform, "Backdrop", Bg, stretch: true);

            var content = CreateImage(canvasGo.transform, "Content", Color.clear, stretch: false);
            content.raycastTarget = false;
            Place(content.rectTransform, 0.08f, 0.12f, 0.55f, 0.88f);

            var title = CreateText(content.transform, "Title", "Station", 42, Accent, FontStyle.Bold);
            Place(title.rectTransform, 0f, 1f, 1f, 1f, new Vector2(0, -90), new Vector2(0, -20));
            title.alignment = TextAnchor.LowerLeft;

            var tagline = CreateText(content.transform, "Tagline", "AgentSession 공유 · 미션을 선택하세요.", 22, TextMuted, FontStyle.Normal);
            Place(tagline.rectTransform, 0f, 1f, 1f, 1f, new Vector2(0, -150), new Vector2(0, -100));
            tagline.alignment = TextAnchor.UpperLeft;

            var buttons = CreateImage(content.transform, "Buttons", Color.clear, stretch: false);
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

            var side = CreateImage(canvasGo.transform, "SidePanel", Panel, stretch: false);
            Place(side.rectTransform, 0.62f, 0.18f, 0.92f, 0.82f);
            var sideTitle = CreateText(side.transform, "SideTitle", "스테이션", 28, TextPrimary, FontStyle.Bold);
            Place(sideTitle.rectTransform, 0.08f, 0.82f, 0.92f, 0.95f);
            sideTitle.alignment = TextAnchor.MiddleLeft;
            var sideBody = CreateText(side.transform, "StationAgentText", "", 20, TextMuted, FontStyle.Normal);
            Place(sideBody.rectTransform, 0.08f, 0.12f, 0.92f, 0.78f);
            sideBody.alignment = TextAnchor.UpperLeft;

            var toast = CreateImage(canvasGo.transform, "StatusToast", AccentDim, stretch: false);
            Place(toast.rectTransform, 0.25f, 0.07f, 0.75f, 0.14f);
            var statusText = CreateText(toast.transform, "StatusText", "", 20, TextPrimary, FontStyle.Normal);
            Stretch(statusText.rectTransform);
            statusText.alignment = TextAnchor.MiddleCenter;
            toast.gameObject.SetActive(false);

            var bdsRoot = CreateImage(canvasGo.transform, "BdsCheckHud", Color.clear, stretch: false);
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
            var bdsLabel = CreateText(bdsBtnGo.transform, "Label", "BDS Check", 20, TextPrimary, FontStyle.Bold);
            Place(bdsLabel.rectTransform, 0.08f, 0.15f, 0.96f, 0.85f);
            bdsLabel.alignment = TextAnchor.MiddleLeft;

            var creditColor = new Color(0.55f, 0.58f, 0.62f, 0.22f);
            var credit = CreateText(canvasGo.transform, "CompanyCredit", "PinkSoft", 18, creditColor, FontStyle.Normal);
            Place(credit.rectTransform, 0.25f, 0.015f, 0.75f, 0.055f);
            credit.alignment = TextAnchor.MiddleCenter;

            var root = new GameObject("PMS_Station");
            var ui = root.AddComponent<StationUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("stationAgentText").objectReferenceValue = sideBody;
            so.FindProperty("selectMissionButton").objectReferenceValue = missionBtn;
            so.FindProperty("logoutButton").objectReferenceValue = logoutBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("bdsCheckButton").objectReferenceValue = bdsBtn;
            so.FindProperty("statusToast").objectReferenceValue = toast.gameObject;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("submitTestResultOnSelect").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EnsureInBuildSettings();
            Debug.Log($"Station scene rebuilt: {ScenePath}");
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

        static Image CreateImage(Transform parent, string name, Color color, bool stretch)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            if (stretch)
                Stretch(img.rectTransform);
            return img;
        }

        static Text CreateText(Transform parent, string name, string value, int size, Color color, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Button CreateButton(Transform parent, string label, Color face)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = face;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 56;
            le.preferredHeight = 64;
            var t = CreateText(go.transform, "Label", label, 22, TextPrimary, FontStyle.Bold);
            Stretch(t.rectTransform);
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return btn;
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

        static void Place(RectTransform rt, float xMin, float yMin, float xMax, float yMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
