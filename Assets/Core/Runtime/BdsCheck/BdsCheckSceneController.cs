using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkSoft.Core.BdsCheck
{
    /// <summary>
    /// BdsCheck 씬 로직만 담당. UI 레이아웃은 Canvas 오브젝트를 인스펙터에서 편집한다.
    /// Teensy R USB HID → TouchInputSource → 5포인트 매칭 (1920×1080).
    /// </summary>
    public sealed class BdsCheckSceneController : MonoBehaviour
    {
        public const int ExpectedScreenWidth = 1920;
        public const int ExpectedScreenHeight = 1080;

        enum Phase
        {
            Intro,
            Checking,
            Summary
        }

        struct PointResult
        {
            public Vector2 ExpectedNorm;
            public Vector2 ActualScreen;
            public float ErrorPx;
            public bool HasHit;
            public bool Passed;
        }

        public const int PointCount = 5;

        static readonly string[] PointLabels = { "중앙", "좌하", "우하", "우상", "좌상" };

        [Header("Flow")]
        [SerializeField] string returnSceneName = "Rendezvous";
        [SerializeField] [Range(0.02f, 0.25f)] float matchRadiusNorm = 0.08f;
        /// <summary>코너 4점 — 화면 네 변에서 동일 픽셀 여백 (1920×1080에서 기본 86px).</summary>
        [SerializeField] float cornerMarginPx = 86f;
        [SerializeField] bool requireAllPoints = true;
        [SerializeField] bool warnIfResolutionMismatch = true;

        [Header("UI — Text")]
        [SerializeField] Text titleText = null!;
        [SerializeField] Text statusText = null!;
        [SerializeField] Text bodyText = null!;

        [Header("UI — Buttons (Intro)")]
        [SerializeField] GameObject introButtonGroup = null!;
        [SerializeField] Button startButton = null!;
        [SerializeField] Button introBackButton = null!;

        [Header("UI — Buttons (Checking)")]
        [SerializeField] GameObject checkingButtonGroup = null!;
        [SerializeField] Button skipButton = null!;
        [SerializeField] Button restartButton = null!;
        [SerializeField] Button abortButton = null!;

        [Header("UI — Buttons (Summary)")]
        [SerializeField] GameObject summaryButtonGroup = null!;
        [SerializeField] Button retryButton = null!;
        [SerializeField] Button doneButton = null!;

        [Header("UI — Markers")]
        [SerializeField] RectTransform targetMarker = null!;
        [SerializeField] Text targetMarkerLabel = null!;
        [SerializeField] RectTransform[] hitMarkers = System.Array.Empty<RectTransform>();
        [SerializeField] Image[] hitMarkerImages = System.Array.Empty<Image>();

        Phase _phase = Phase.Intro;
        PointResult[] _results = System.Array.Empty<PointResult>();
        int _pointIndex;
        bool _acceptingHits;
        bool _closed;
        bool _spawnedLocalBds;
        BdsService? _bds;
        IInputSource? _input;

        float MatchRadiusPx => matchRadiusNorm * Mathf.Min(Screen.width, Screen.height);

        void Awake()
        {
            WireButtons();
        }

        void Start()
        {
            _results = new PointResult[PointCount];
            ResetResults();

            if (warnIfResolutionMismatch &&
                (Screen.width != ExpectedScreenWidth || Screen.height != ExpectedScreenHeight))
            {
                Debug.LogWarning(
                    $"BdsCheck: Screen={Screen.width}x{Screen.height} — Teensy HID 기준은 {ExpectedScreenWidth}x{ExpectedScreenHeight}.");
            }

            _bds = EnsureBdsService();
            if (_bds == null)
            {
                SetTexts("BDS Check", "BdsService 초기화 실패", "Boot 또는 단독 생성에 실패했습니다.");
                ShowButtonGroup(null);
                return;
            }

            _bds.EnterCalibrationMode();
            _input = _bds.ActiveInput;
            if (_input != null)
                _input.OnHit += OnInputHit;

            SetPhase(Phase.Intro);
        }

        void OnDestroy()
        {
            DetachInput();
            if (!_closed)
                _bds?.ExitCalibrationMode();
        }

        void WireButtons()
        {
            Bind(startButton, BeginCheck);
            Bind(introBackButton, ReturnToRendezvous);
            Bind(skipButton, SkipCurrentPoint);
            Bind(restartButton, BeginCheck);
            Bind(abortButton, ReturnToRendezvous);
            Bind(retryButton, BeginCheck);
            Bind(doneButton, ReturnToRendezvous);
        }

        static void Bind(Button? button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        BdsService EnsureBdsService()
        {
            if (BdsService.Instance != null)
                return BdsService.Instance;

            var go = new GameObject("PMS_BdsService_Local");
            var service = go.AddComponent<BdsService>();
            _spawnedLocalBds = true;
            Debug.Log("BdsCheck: Boot 없이 단독 실행 — BdsService 로컬 생성");
            return service;
        }

        void SetPhase(Phase phase)
        {
            _phase = phase;
            RefreshUi();
        }

        void RefreshUi()
        {
            var statusLine = BuildStatusLine();

            switch (_phase)
            {
                case Phase.Intro:
                    SetTexts(
                        "BDS Check — Teensy R HID",
                        statusLine,
                        "HID 통과 좌표를 5포인트와 비교합니다.\nTeensy Mouse.moveTo + click → 이 화면\n아래 버튼으로 시작하세요.");
                    ShowButtonGroup(introButtonGroup);
                    SetTargetMarkerVisible(false);
                    break;

                case Phase.Checking:
                {
                    var label = PointLabels[Mathf.Clamp(_pointIndex, 0, PointLabels.Length - 1)];
                    var expected = ExpectedScreen(_pointIndex);
                    SetTexts(
                        $"포인트 {_pointIndex + 1} / {PointCount} — {label}",
                        statusLine,
                        $"표시된 십자를 통과하세요.\n목표 ({expected.x:F0}, {expected.y:F0})");
                    ShowButtonGroup(checkingButtonGroup);
                    SetTargetMarkerVisible(true);
                    PlaceNormalized(targetMarker, GetCheckPointNorm(_pointIndex));
                    if (targetMarkerLabel != null)
                        targetMarkerLabel.text = $"{_pointIndex + 1}/{PointCount} {label}";
                    break;
                }

                case Phase.Summary:
                    SetTexts(
                        IsOverallPass(CountPassed()) ? "결과 · BDS 정상" : "결과 · BDS 문제 가능",
                        statusLine,
                        BuildSummaryBody());
                    ShowButtonGroup(summaryButtonGroup);
                    SetTargetMarkerVisible(false);
                    break;
            }

            RefreshHitMarkers();
        }

        string BuildStatusLine()
        {
            var input = _bds != null ? _bds.GetHardwareStatus().InputSourceName : "none";
            var line =
                $"{ExpectedScreenWidth}×{ExpectedScreenHeight} 기준 · Screen {Screen.width}×{Screen.height}\n" +
                $"입력 {input} · 허용 ±{MatchRadiusPx:F0}px";
            if (_spawnedLocalBds)
                line += "\n단독 실행 (Boot 미경유)";
            return line;
        }

        string BuildSummaryBody()
        {
            var passed = CountPassed();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"통과 {passed} / {PointCount} (허용 {MatchRadiusPx:F0}px)");
            sb.AppendLine();
            for (var i = 0; i < _results.Length; i++)
            {
                var r = _results[i];
                var exp = NormToScreen(r.ExpectedNorm);
                if (!r.HasHit)
                    sb.AppendLine($"{i + 1}. {PointLabels[i]} 미측정  목표({exp.x:F0},{exp.y:F0})");
                else
                {
                    var mark = r.Passed ? "OK" : "FAIL";
                    sb.AppendLine(
                        $"{i + 1}. {PointLabels[i]} [{mark}]  목표({exp.x:F0},{exp.y:F0})  실제({r.ActualScreen.x:F0},{r.ActualScreen.y:F0})  Δ{r.ErrorPx:F0}");
                }
            }

            return sb.ToString();
        }

        void SetTexts(string title, string status, string body)
        {
            if (titleText != null)
                titleText.text = title;
            if (statusText != null)
                statusText.text = status;
            if (bodyText != null)
                bodyText.text = body;
        }

        void ShowButtonGroup(GameObject? active)
        {
            SetActive(introButtonGroup, active == introButtonGroup);
            SetActive(checkingButtonGroup, active == checkingButtonGroup);
            SetActive(summaryButtonGroup, active == summaryButtonGroup);
        }

        static void SetActive(GameObject? go, bool on)
        {
            if (go != null)
                go.SetActive(on);
        }

        void SetTargetMarkerVisible(bool on)
        {
            if (targetMarker != null)
                targetMarker.gameObject.SetActive(on);
        }

        void RefreshHitMarkers()
        {
            for (var i = 0; i < hitMarkers.Length; i++)
            {
                var rt = hitMarkers[i];
                if (rt == null)
                    continue;

                var show = i < _results.Length && _results[i].HasHit;
                rt.gameObject.SetActive(show);
                if (!show)
                    continue;

                var r = _results[i];
                PlaceScreenPixel(rt, r.ActualScreen);
                if (i < hitMarkerImages.Length && hitMarkerImages[i] != null)
                {
                    hitMarkerImages[i].color = r.Passed
                        ? new Color(0.3f, 0.85f, 0.45f, 0.95f)
                        : new Color(0.95f, 0.35f, 0.3f, 0.95f);
                }
            }
        }

        static void PlaceNormalized(RectTransform? rt, Vector2 norm)
        {
            if (rt == null)
                return;
            rt.anchorMin = rt.anchorMax = norm;
            rt.anchoredPosition = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static void PlaceScreenPixel(RectTransform? rt, Vector2 screenBottomLeft)
        {
            if (rt == null)
                return;
            var norm = new Vector2(
                screenBottomLeft.x / Mathf.Max(Screen.width, 1),
                screenBottomLeft.y / Mathf.Max(Screen.height, 1));
            PlaceNormalized(rt, norm);
        }

        /// <summary>
        /// 코너 여백은 네 변 동일 픽셀. 정규화 u/v는 해상도 비율에 맞게 따로 계산.
        /// </summary>
        Vector2 GetCheckPointNorm(int index)
        {
            var w = Mathf.Max(Screen.width, 1);
            var h = Mathf.Max(Screen.height, 1);
            var margin = Mathf.Clamp(cornerMarginPx, 0f, Mathf.Min(w, h) * 0.45f);
            var u = margin / w;
            var v = margin / h;

            return index switch
            {
                0 => new Vector2(0.5f, 0.5f),
                1 => new Vector2(u, v),
                2 => new Vector2(1f - u, v),
                3 => new Vector2(1f - u, 1f - v),
                4 => new Vector2(u, 1f - v),
                _ => new Vector2(0.5f, 0.5f)
            };
        }

        Vector2 ExpectedScreen(int index) => NormToScreen(GetCheckPointNorm(index));

        static Vector2 NormToScreen(Vector2 norm) =>
            new(norm.x * Screen.width, norm.y * Screen.height);

        public void BeginCheck()
        {
            _phase = Phase.Checking;
            _pointIndex = 0;
            _acceptingHits = true;
            ResetResults();
            RefreshUi();
        }

        void SkipCurrentPoint()
        {
            if (_phase != Phase.Checking)
                return;
            AdvancePoint();
            RefreshUi();
        }

        void ResetResults()
        {
            for (var i = 0; i < PointCount; i++)
                _results[i] = new PointResult { ExpectedNorm = GetCheckPointNorm(i) };
        }

        void OnInputHit(InputHit hit)
        {
            if (!_acceptingHits || _phase != Phase.Checking)
                return;
            if (_pointIndex < 0 || _pointIndex >= _results.Length)
                return;
            if (_results[_pointIndex].HasHit)
                return;

            var expected = ExpectedScreen(_pointIndex);
            var error = Vector2.Distance(hit.ScreenPosition, expected);
            _results[_pointIndex] = new PointResult
            {
                ExpectedNorm = GetCheckPointNorm(_pointIndex),
                ActualScreen = hit.ScreenPosition,
                ErrorPx = error,
                HasHit = true,
                Passed = error <= MatchRadiusPx
            };
            AdvancePoint();
            RefreshUi();
        }

        void AdvancePoint()
        {
            _pointIndex++;
            if (_pointIndex >= PointCount)
            {
                _acceptingHits = false;
                _phase = Phase.Summary;
            }
        }

        int CountPassed()
        {
            var n = 0;
            foreach (var r in _results)
            {
                if (r.HasHit && r.Passed)
                    n++;
            }

            return n;
        }

        bool IsOverallPass(int passed) =>
            requireAllPoints ? passed >= PointCount : passed >= PointCount - 1;

        void DetachInput()
        {
            if (_input != null)
            {
                _input.OnHit -= OnInputHit;
                _input = null;
            }
        }

        public void ReturnToRendezvous()
        {
            if (_closed)
                return;
            _closed = true;
            _acceptingHits = false;
            DetachInput();
            _bds?.ExitCalibrationMode();
            SceneManager.LoadScene(returnSceneName);
        }
    }
}
