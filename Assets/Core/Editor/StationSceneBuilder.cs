using PinkSoft.Core.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PinkSoft.EditorTools
{
    /// <summary>
    /// Station 씬 — 가운데 추천 카드 + 하단 가로 앨범 스트립.
    /// </summary>
    public static class StationSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Station.unity";

        static readonly Color Bg = new(0.05f, 0.07f, 0.09f, 1f);
        static readonly Color Panel = new(0.09f, 0.12f, 0.15f, 0.92f);
        static readonly Color Card = new(0.12f, 0.15f, 0.18f, 0.95f);
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

            var backdrop = CreateImage(canvasGo.transform, "Backdrop", Color.white, stretch: true);
            var bgTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Core/Runtime/Lobby/UI/station_table_sketches_bg.png");
            var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Core/Runtime/Lobby/UI/station_table_sketches_bg.png");
            if (bgSprite != null)
            {
                backdrop.sprite = bgSprite;
                backdrop.type = Image.Type.Simple;
                backdrop.preserveAspect = false;
                backdrop.color = Color.white;
            }
            else if (bgTex != null)
            {
                // Sprite import 전이면 텍스처로 임시 스프라이트 생성은 불가 — 단색 폴백
                backdrop.color = new Color(0.08f, 0.09f, 0.10f, 1f);
                Debug.LogWarning("station_table_sketches_bg Sprite import 필요 (Texture Type = Sprite).");
            }

            // 가독성용 약한 딤 (배경 사진 위 UI)
            var dim = CreateImage(canvasGo.transform, "BackdropDim", new Color(0.02f, 0.03f, 0.04f, 0.35f), stretch: true);
            dim.raycastTarget = false;

            // —— 미션 목록 (가운데 위, 좌우 동일 마진) ——
            var stripFrame = CreateImage(canvasGo.transform, "AlbumStrip", new Color(0.07f, 0.09f, 0.11f, 0.9f), stretch: false);
            Place(stripFrame.rectTransform, 0.05f, 0.52f, 0.95f, 0.90f);

            var stripLabel = CreateText(stripFrame.transform, "StripLabel", "미션 목록 — 탭하여 선택", 16, TextMuted, FontStyle.Normal);
            Place(stripLabel.rectTransform, 0.02f, 0.86f, 0.98f, 0.98f);
            stripLabel.alignment = TextAnchor.MiddleLeft;

            var scrollGo = new GameObject("StripScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(stripFrame.transform, false);
            Place(scrollGo.GetComponent<RectTransform>(), 0.015f, 0.04f, 0.985f, 0.84f);
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0, 0, 0, 0.01f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<Image>().color = Color.white;
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 0f);
            contentRt.anchorMax = new Vector2(0f, 1f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.sizeDelta = new Vector2(0f, 0f);
            var hlg = content.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;

            // 타일 프리팹 (비활성, 런타임 Instantiate)
            var tilePrefab = CreateTilePrefab(canvasGo.transform);
            tilePrefab.SetActive(false);

            // —— 하단 행: 파티(좌) / 미션 상세(우) — 동일 높이 ——
            const float bottomYMin = 0.08f;
            const float bottomYMax = 0.48f;
            const float gap = 0.02f;
            const float sideMargin = 0.05f;
            const float mid = 0.50f;

            // 파티 (왼쪽 아래)
            var side = CreateImage(canvasGo.transform, "SidePanel", Panel, stretch: false);
            Place(side.rectTransform, sideMargin, bottomYMin, mid - gap * 0.5f, bottomYMax);
            var sideTitle = CreateText(side.transform, "SideTitle", "파티", 22, TextPrimary, FontStyle.Bold);
            Place(sideTitle.rectTransform, 0.06f, 0.84f, 0.94f, 0.96f);
            sideTitle.alignment = TextAnchor.MiddleLeft;
            var sideBody = CreateText(side.transform, "StationAgentText", "", 16, TextMuted, FontStyle.Normal);
            Place(sideBody.rectTransform, 0.06f, 0.28f, 0.94f, 0.82f);
            sideBody.alignment = TextAnchor.UpperLeft;

            var sideButtons = CreateImage(side.transform, "SideButtons", Color.clear, stretch: false);
            sideButtons.raycastTarget = false;
            Place(sideButtons.rectTransform, 0.06f, 0.04f, 0.94f, 0.24f);
            var hBtn = sideButtons.gameObject.AddComponent<HorizontalLayoutGroup>();
            hBtn.spacing = 10;
            hBtn.childControlHeight = true;
            hBtn.childControlWidth = true;
            hBtn.childForceExpandHeight = true;
            hBtn.childForceExpandWidth = true;
            var logoutBtn = CreateButton(sideButtons.transform, "클리어런스 해제", ButtonFace);
            var quitBtn = CreateButton(sideButtons.transform, "종료", ButtonFace);
            logoutBtn.GetComponent<LayoutElement>().preferredHeight = 40;
            quitBtn.GetComponent<LayoutElement>().preferredHeight = 40;

            // 미션 상세 (오른쪽 아래, 축소)
            var featured = CreateImage(canvasGo.transform, "FeaturedCard", Card, stretch: false);
            Place(featured.rectTransform, mid + gap * 0.5f, bottomYMin, 1f - sideMargin, bottomYMax);
            var featuredOutline = featured.gameObject.AddComponent<Outline>();
            featuredOutline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.55f);
            featuredOutline.effectDistance = new Vector2(2f, -2f);

            var badge = CreateText(featured.transform, "Badge", "추천", 15, Accent, FontStyle.Bold);
            Place(badge.rectTransform, 0.06f, 0.86f, 0.50f, 0.96f);
            badge.alignment = TextAnchor.MiddleLeft;

            var fTitle = CreateText(featured.transform, "Title", "미션 제목", 26, TextPrimary, FontStyle.Bold);
            Place(fTitle.rectTransform, 0.06f, 0.68f, 0.94f, 0.86f);
            fTitle.alignment = TextAnchor.LowerLeft;

            var fBody = CreateText(featured.transform, "Body", "설명", 16, TextMuted, FontStyle.Normal);
            Place(fBody.rectTransform, 0.06f, 0.36f, 0.94f, 0.66f);
            fBody.alignment = TextAnchor.UpperLeft;

            var fMeta = CreateText(featured.transform, "Meta", "meta", 14, TextMuted, FontStyle.Normal);
            Place(fMeta.rectTransform, 0.06f, 0.22f, 0.94f, 0.36f);
            fMeta.alignment = TextAnchor.UpperLeft;

            var deployBtn = CreateButton(featured.transform, "투입", Accent);
            Place(deployBtn.GetComponent<RectTransform>(), 0.06f, 0.05f, 0.48f, 0.18f);
            Object.DestroyImmediate(deployBtn.GetComponent<LayoutElement>());

            // Toast / BDS / credit
            var toast = CreateImage(canvasGo.transform, "StatusToast", AccentDim, stretch: false);
            Place(toast.rectTransform, 0.25f, 0.01f, 0.75f, 0.065f);
            var statusText = CreateText(toast.transform, "StatusText", "", 20, TextPrimary, FontStyle.Normal);
            Stretch(statusText.rectTransform);
            statusText.alignment = TextAnchor.MiddleCenter;
            toast.gameObject.SetActive(false);

            var bdsRoot = CreateImage(canvasGo.transform, "BdsCheckHud", Color.clear, stretch: false);
            bdsRoot.raycastTarget = false;
            Place(bdsRoot.rectTransform, 0.78f, 0.90f, 0.98f, 0.98f);
            var bdsBtnGo = new GameObject("BdsCheckButton", typeof(RectTransform), typeof(Image), typeof(Button));
            bdsBtnGo.transform.SetParent(bdsRoot.transform, false);
            Stretch(bdsBtnGo.GetComponent<RectTransform>());
            var bdsImg = bdsBtnGo.GetComponent<Image>();
            bdsImg.color = new Color(0.10f, 0.14f, 0.17f, 0.95f);
            var bdsBtn = bdsBtnGo.GetComponent<Button>();
            bdsBtn.targetGraphic = bdsImg;
            var bdsLabel = CreateText(bdsBtnGo.transform, "Label", "BDS Check", 18, TextPrimary, FontStyle.Bold);
            Place(bdsLabel.rectTransform, 0.08f, 0.15f, 0.96f, 0.85f);
            bdsLabel.alignment = TextAnchor.MiddleLeft;

            var credit = CreateText(canvasGo.transform, "CompanyCredit", "PinkSoft", 16,
                new Color(0.55f, 0.58f, 0.62f, 0.22f), FontStyle.Normal);
            Place(credit.rectTransform, 0.35f, 0.005f, 0.65f, 0.04f);
            credit.alignment = TextAnchor.MiddleCenter;

            // —— Album + StationUI wiring ——
            var albumGo = new GameObject("MissionAlbum");
            albumGo.transform.SetParent(canvasGo.transform, false);
            var album = albumGo.AddComponent<MissionAlbumView>();
            var albumSo = new SerializedObject(album);
            albumSo.FindProperty("featuredBadgeText").objectReferenceValue = badge;
            albumSo.FindProperty("featuredTitleText").objectReferenceValue = fTitle;
            albumSo.FindProperty("featuredBodyText").objectReferenceValue = fBody;
            albumSo.FindProperty("featuredMetaText").objectReferenceValue = fMeta;
            albumSo.FindProperty("featuredCardImage").objectReferenceValue = featured;
            albumSo.FindProperty("deployButton").objectReferenceValue = deployBtn;
            albumSo.FindProperty("stripContent").objectReferenceValue = contentRt;
            albumSo.FindProperty("stripScroll").objectReferenceValue = scroll;
            albumSo.FindProperty("tilePrefab").objectReferenceValue = tilePrefab;
            albumSo.ApplyModifiedPropertiesWithoutUndo();

            var root = new GameObject("PMS_Station");
            var ui = root.AddComponent<StationUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("stationAgentText").objectReferenceValue = sideBody;
            so.FindProperty("missionAlbum").objectReferenceValue = album;
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
            Debug.Log($"Station album UI rebuilt: {ScenePath}");
        }

        static GameObject CreateTilePrefab(Transform parent)
        {
            var go = new GameObject("MissionTilePrefab", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 120f);
            var img = go.GetComponent<Image>();
            img.color = Card;
            var le = go.GetComponent<LayoutElement>();
            le.minWidth = 200f;
            le.preferredWidth = 200f;
            le.minHeight = 110f;
            le.preferredHeight = 120f;

            var title = CreateText(go.transform, "Title", "Title", 20, TextPrimary, FontStyle.Bold);
            Place(title.rectTransform, 0.08f, 0.45f, 0.92f, 0.88f);
            title.alignment = TextAnchor.LowerLeft;
            title.raycastTarget = false;

            var sub = CreateText(go.transform, "Sub", "category", 14, TextMuted, FontStyle.Normal);
            Place(sub.rectTransform, 0.08f, 0.12f, 0.92f, 0.42f);
            sub.alignment = TextAnchor.UpperLeft;
            sub.raycastTarget = false;

            return go;
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
            le.minHeight = 48;
            le.preferredHeight = 56;
            var t = CreateText(go.transform, "Label", label, 20, TextPrimary, FontStyle.Bold);
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
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
