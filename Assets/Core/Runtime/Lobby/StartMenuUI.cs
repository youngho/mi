using PinkSoft.Core;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 접선(Clearance)에서 최대 4명 등록 → 별도 버튼으로 Station 진입.
    /// Nobody는 Guest 콜사인으로 파티에 추가만 하며, Station으로 자동 전환하지 않는다.
    /// BDS Check는 전용 씬으로 전환한다 (이 UI에서 검증 로직을 갖지 않음).
    /// </summary>
    public sealed class StartMenuUI : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] GameObject identityPanel = null!;
        [SerializeField] GameObject stationPanel = null!;

        [Header("Identity / Rendezvous")]
        [SerializeField] InputField callsignInput = null!;
        [SerializeField] Button confirmIdentityButton = null!;
        [SerializeField] Button nobodyButton = null!;
        [SerializeField] Button enterStationButton = null!;
        [SerializeField] Text identityStatusText = null!;
        [SerializeField] Text[] partySlotTexts = System.Array.Empty<Text>();
        [SerializeField] Image[] partySlotBackgrounds = System.Array.Empty<Image>();
        [SerializeField] GameObject[] partySlotRoots = System.Array.Empty<GameObject>();
        [SerializeField] RawImage[] partySlotPortraits = System.Array.Empty<RawImage>();
        [SerializeField] Texture2D nobodyPortraitTexture = null!;
        [SerializeField] PinkSoftApiClient apiClient = null!;
        [SerializeField] bool allowOfflineClearance = true;

        [Header("Station")]
        [SerializeField] Button selectMissionButton = null!;
        [SerializeField] Button quitButton = null!;
        [SerializeField] Button logoutButton = null!;
        [SerializeField] Text stationAgentText = null!;
        [SerializeField] GameObject statusToast = null!;
        [SerializeField] Text statusText = null!;
        [SerializeField] bool hideCursor;

        [Header("System — always visible")]
        [SerializeField] Button bdsCheckButton = null!;
        [SerializeField] GameObject bdsCheckRoot = null!;
        [SerializeField] string bdsCheckSceneName = "BdsCheck";

        bool _busy;
        Texture2D? _nobodyPortraitResolved;

        void Awake()
        {
            EnsureAgentSession();
            EnsurePortraitBindings();

            if (apiClient == null)
                apiClient = FindAnyObjectByType<PinkSoftApiClient>();

            if (hideCursor)
                Cursor.visible = false;

            if (statusToast != null)
                statusToast.SetActive(false);

            WireButtons();
            RefreshFlow();
        }

        static void EnsureAgentSession()
        {
            if (AgentSession.Instance != null)
                return;

            var go = new GameObject("AgentSession");
            go.AddComponent<AgentSession>();
        }

        void WireButtons()
        {
            Bind(confirmIdentityButton, OnConfirmIdentity);
            Bind(nobodyButton, OnAddNobody);
            Bind(enterStationButton, OnEnterStation);
            Bind(selectMissionButton, OnSelectMission);
            Bind(bdsCheckButton, OnOpenBdsCheck);
            Bind(quitButton, OnQuit);
            Bind(logoutButton, OnLogout);
        }

        static void Bind(Button? button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        void RefreshFlow()
        {
            var session = AgentSession.Instance;
            var atStation = session != null && session.IsAtStation;

            if (identityPanel != null)
                identityPanel.SetActive(!atStation);
            if (stationPanel != null)
                stationPanel.SetActive(atStation);
            if (bdsCheckRoot != null)
                bdsCheckRoot.SetActive(true);

            if (atStation)
                UpdateStationPanel();
            else
                UpdateRendezvousPanel();
        }

        void UpdateRendezvousPanel()
        {
            var session = AgentSession.Instance;
            _busy = false;
            SetIdentityButtonsInteractable(true);
            RefreshPartyListUi(session);

            if (enterStationButton != null)
                enterStationButton.interactable = session != null && session.CanEnterStation;

            if (nobodyButton != null)
                nobodyButton.interactable = session == null || !session.IsPartyFull;
        }

        void RefreshPartyListUi(AgentSession? session)
        {
            EnsurePortraitBindings();

            var filled = new Color(0.16f, 0.22f, 0.28f, 0.85f);
            var filledText = new Color(0.95f, 0.94f, 0.92f, 1f);
            var nobodyAccent = new Color(0.95f, 0.72f, 0.55f, 1f);
            var portrait = ResolveNobodyPortrait();

            for (var i = 0; i < AgentSession.MaxAgents; i++)
            {
                var occupied = session != null && i < session.PartyCount;

                if (i < partySlotRoots.Length && partySlotRoots[i] != null)
                    partySlotRoots[i].SetActive(occupied);

                if (!occupied)
                {
                    if (i < partySlotPortraits.Length && partySlotPortraits[i] != null)
                        partySlotPortraits[i].enabled = false;
                    continue;
                }

                if (i < partySlotBackgrounds.Length && partySlotBackgrounds[i] != null)
                    partySlotBackgrounds[i].color = filled;

                var slot = session!.Party[i];
                var isNobody = slot.IsNobody;

                if (i < partySlotTexts.Length && partySlotTexts[i] != null)
                {
                    if (isNobody)
                    {
                        partySlotTexts[i].text = "Nobody\nGuest · 기본값";
                        partySlotTexts[i].color = nobodyAccent;
                    }
                    else
                    {
                        partySlotTexts[i].text = $"{slot.User.nickname}\nLv.{slot.User.currentLevel}";
                        partySlotTexts[i].color = filledText;
                    }
                }

                if (i < partySlotPortraits.Length && partySlotPortraits[i] != null)
                {
                    if (isNobody && portrait != null)
                    {
                        partySlotPortraits[i].texture = portrait;
                        partySlotPortraits[i].enabled = true;
                        partySlotPortraits[i].color = Color.white;
                    }
                    else
                    {
                        partySlotPortraits[i].enabled = false;
                    }
                }
            }
        }

        Texture2D? ResolveNobodyPortrait()
        {
            if (nobodyPortraitTexture != null)
                return nobodyPortraitTexture;

            if (_nobodyPortraitResolved == null)
                _nobodyPortraitResolved = Resources.Load<Texture2D>("PartyPortrait/NobodyPortrait");

            return _nobodyPortraitResolved;
        }

        void EnsurePortraitBindings()
        {
            if (partySlotPortraits == null || partySlotPortraits.Length != AgentSession.MaxAgents)
                partySlotPortraits = new RawImage[AgentSession.MaxAgents];

            for (var i = 0; i < AgentSession.MaxAgents; i++)
            {
                if (i >= partySlotRoots.Length || partySlotRoots[i] == null)
                    continue;

                var root = partySlotRoots[i];

                // 기존 씬의 얇은 파티 바를 초상 카드 높이로 확장
                var partyPanel = root.transform.parent != null ? root.transform.parent.parent as RectTransform : null;
                if (partyPanel != null && partyPanel.name == "PartyPanel")
                {
                    partyPanel.anchorMin = new Vector2(0.05f, 2f / 3f - 0.16f);
                    partyPanel.anchorMax = new Vector2(0.95f, 2f / 3f + 0.14f);
                    partyPanel.offsetMin = Vector2.zero;
                    partyPanel.offsetMax = Vector2.zero;
                }

                var le = root.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = 140;
                    le.preferredWidth = 168;
                    le.minHeight = 200;
                    le.preferredHeight = 220;
                }

                if (partySlotPortraits[i] == null)
                {
                    var existing = root.transform.Find("Portrait");
                    if (existing != null)
                        partySlotPortraits[i] = existing.GetComponent<RawImage>();
                }

                if (partySlotPortraits[i] == null)
                {
                    var go = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                    go.transform.SetParent(root.transform, false);
                    go.transform.SetAsFirstSibling();
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.08f, 0.28f);
                    rt.anchorMax = new Vector2(0.92f, 0.94f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    var raw = go.GetComponent<RawImage>();
                    raw.raycastTarget = false;
                    raw.color = Color.white;
                    partySlotPortraits[i] = raw;
                }

                if (i < partySlotTexts.Length && partySlotTexts[i] != null)
                {
                    var labelRt = partySlotTexts[i].rectTransform;
                    labelRt.anchorMin = new Vector2(0.04f, 0.02f);
                    labelRt.anchorMax = new Vector2(0.96f, 0.28f);
                    labelRt.offsetMin = Vector2.zero;
                    labelRt.offsetMax = Vector2.zero;
                    partySlotTexts[i].alignment = TextAnchor.MiddleCenter;
                    partySlotTexts[i].fontSize = 16;
                }
            }
        }

        void SetIdentityButtonsInteractable(bool value)
        {
            if (confirmIdentityButton != null)
                confirmIdentityButton.interactable = value;
            if (nobodyButton != null)
            {
                var session = AgentSession.Instance;
                nobodyButton.interactable = value && (session == null || !session.IsPartyFull);
            }
        }

        public void OnConfirmIdentity()
        {
            if (_busy)
                return;

            var session = AgentSession.Instance;
            if (session == null)
                return;

            if (session.IsPartyFull)
            {
                SetIdentityStatus($"파티가 가득 찼습니다 ({AgentSession.MaxAgents}명).");
                return;
            }

            var callsign = callsignInput != null ? callsignInput.text.Trim() : "";
            if (string.IsNullOrEmpty(callsign))
            {
                SetIdentityStatus("콜사인이 비어 있습니다.");
                return;
            }

            if (string.Equals(callsign, AgentSession.NobodyNickname, System.StringComparison.OrdinalIgnoreCase))
            {
                OnAddNobody();
                return;
            }

            _busy = true;
            SetIdentityButtonsInteractable(false);
            SetIdentityStatus("신원 확인 중…");

            if (apiClient != null)
            {
                StartCoroutine(apiClient.Login(callsign, ok =>
                {
                    if (ok && apiClient.UserId != null)
                    {
                        AddAgentToParty(new RuntimeUserData
                        {
                            userId = apiClient.UserId,
                            nickname = callsign,
                            currentLevel = 1
                        }, isNobody: false, "클리어런스 승인 — 파티에 추가됨");
                        return;
                    }

                    if (allowOfflineClearance)
                    {
                        AddAgentToParty(new RuntimeUserData
                        {
                            userId = $"local:{callsign}",
                            nickname = callsign,
                            currentLevel = 1
                        }, isNobody: false, "오프라인 클리어런스 — 파티에 추가됨 (정식 가입은 앱)");
                    }
                    else
                    {
                        SetIdentityStatus("등록된 콜사인이 없습니다. 회원가입은 앱에서 한 뒤 다시 시도하세요.");
                        _busy = false;
                        SetIdentityButtonsInteractable(true);
                    }
                }));
                return;
            }

            if (allowOfflineClearance)
            {
                AddAgentToParty(new RuntimeUserData
                {
                    userId = $"local:{callsign}",
                    nickname = callsign,
                    currentLevel = 1
                }, isNobody: false, "로컬 클리어런스 — 파티에 추가됨");
            }
            else
            {
                SetIdentityStatus("API 클라이언트가 없습니다.");
                _busy = false;
                SetIdentityButtonsInteractable(true);
            }
        }

        /// <summary>계정 없는 친구용 Guest — 파티에만 추가. Station으로 자동 이동하지 않음.</summary>
        public void OnAddNobody()
        {
            if (_busy)
                return;

            var session = AgentSession.Instance;
            if (session == null)
                return;

            var result = session.TryAddNobody();
            HandleAddResult(result, "Nobody(Guest) — 시스템 기본값으로 파티에 추가됨");
        }

        void AddAgentToParty(RuntimeUserData user, bool isNobody, string okMessage)
        {
            var session = AgentSession.Instance!;
            var result = session.TryAddAgent(user, isNobody);
            HandleAddResult(result, okMessage);
        }

        void HandleAddResult(AgentSession.AddResult result, string okMessage)
        {
            _busy = false;
            SetIdentityButtonsInteractable(true);

            switch (result)
            {
                case AgentSession.AddResult.Ok:
                    if (callsignInput != null)
                        callsignInput.text = "";
                    SetIdentityStatus("등록됨. 더 추가하거나 Station에 진입하세요.");
                    ShowStatus(okMessage);
                    UpdateRendezvousPanel();
                    break;
                case AgentSession.AddResult.PartyFull:
                    SetIdentityStatus($"파티가 가득 찼습니다 ({AgentSession.MaxAgents}명).");
                    ShowStatus("파티 정원 초과");
                    break;
                case AgentSession.AddResult.DuplicateCallsign:
                    SetIdentityStatus("이미 등록된 콜사인입니다.");
                    ShowStatus("중복 콜사인");
                    break;
                default:
                    SetIdentityStatus("등록에 실패했습니다.");
                    break;
            }
        }

        /// <summary>접선 완료 — 파티가 1명 이상일 때만 Station으로 전환.</summary>
        public void OnEnterStation()
        {
            var session = AgentSession.Instance;
            if (session == null || !session.HasParty)
            {
                SetIdentityStatus("먼저 에이전트를 1명 이상 등록하세요.");
                ShowStatus("파티가 비어 있습니다");
                return;
            }

            session.EnterStation();
            RefreshFlow();
            ShowStatus($"Station 진입 — 파티 {session.PartyCount}명");
        }

        void UpdateStationPanel()
        {
            var session = AgentSession.Instance;
            if (stationAgentText == null || session == null)
                return;

            stationAgentText.text =
                session.BuildPartySummary() +
                "\n\n미션을 선택하세요.\n(BDS Check는 우측 상단)";
        }

        public void OnSelectMission()
        {
            var session = AgentSession.Instance;
            if (session == null || !session.IsAtStation)
            {
                ShowStatus("먼저 Station에 진입하세요.");
                return;
            }

            ShowStatus(session.IsNobody
                ? "Nobody 포함 파티 — 시스템 기본 설정으로 미션 연동 예정"
                : "미션 실행 연동은 다음 단계에서 붙입니다.");
        }

        public void OnOpenBdsCheck()
        {
            if (string.IsNullOrWhiteSpace(bdsCheckSceneName))
            {
                ShowStatus("BDS Check 씬 이름이 비어 있습니다.");
                return;
            }

            SceneManager.LoadScene(bdsCheckSceneName);
        }

        public void OnLogout()
        {
            AgentSession.Instance?.Revoke();
            RefreshFlow();
            SetIdentityStatus("파티가 해제되었습니다. 다시 접선하세요.");
            ShowStatus("접선 화면으로 복귀");
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetIdentityStatus(string message)
        {
            if (identityStatusText != null)
                identityStatusText.text = message;
        }

        void ShowStatus(string message)
        {
            if (statusToast == null || statusText == null)
                return;

            statusText.text = message;
            statusToast.SetActive(true);
            CancelInvoke(nameof(HideStatus));
            Invoke(nameof(HideStatus), 2.5f);
        }

        void HideStatus()
        {
            if (statusToast != null)
                statusToast.SetActive(false);
        }
    }
}
