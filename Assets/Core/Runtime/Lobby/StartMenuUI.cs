using PinkSoft.Core;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 접선(Clearance) 씬 UI. 파티 등록 후 Station 씬으로 전환한다.
    /// 에이전트·API는 <see cref="AgentSession"/> / <see cref="PinkSoftApiClient"/> (DDOL)에 보관한다.
    /// </summary>
    public sealed class StartMenuUI : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] GameObject identityPanel = null!;

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
        [SerializeField] string bayId = "bay-local-1";

        [Header("Shared HUD")]
        [SerializeField] GameObject statusToast = null!;
        [SerializeField] Text statusText = null!;
        [SerializeField] bool hideCursor;
        [SerializeField] Button bdsCheckButton = null!;
        [SerializeField] GameObject bdsCheckRoot = null!;

        bool _busy;
        Texture2D? _nobodyPortraitResolved;

        void Awake()
        {
            var session = AgentSession.Ensure();
            session.SetBayId(bayId);
            session.LeaveStation();

            // 씬에 붙은 ApiClient 설정을 DDOL 인스턴스로 흡수
            var sharedApi = PinkSoftApiClient.EnsureOn(session.gameObject);
            if (apiClient != null && apiClient != sharedApi)
                sharedApi.CopySettingsFrom(apiClient);
            apiClient = sharedApi;

            EnsurePortraitBindings();

            if (hideCursor)
                Cursor.visible = false;

            if (statusToast != null)
                statusToast.SetActive(false);

            // 구 StationPanel이 씬에 남아 있으면 숨김 (씬 분리 후)
            var leftover = GameObject.Find("StationPanel");
            if (leftover != null)
                leftover.SetActive(false);

            WireButtons();
            RefreshFlow();
        }

        void WireButtons()
        {
            Bind(confirmIdentityButton, OnConfirmIdentity);
            Bind(nobodyButton, OnAddNobody);
            Bind(enterStationButton, OnEnterStation);
            Bind(bdsCheckButton, OnOpenBdsCheck);
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
            if (identityPanel != null)
                identityPanel.SetActive(true);
            if (bdsCheckRoot != null)
                bdsCheckRoot.SetActive(true);

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

            var session = AgentSession.Require();
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

            var api = PinkSoftApiClient.Instance ?? apiClient;
            if (api != null)
            {
                StartCoroutine(api.Login(callsign, ok =>
                {
                    if (ok && api.UserId != null)
                    {
                        AddAgentToParty(new RuntimeUserData
                        {
                            userId = api.UserId,
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

        public void OnAddNobody()
        {
            if (_busy)
                return;

            var result = AgentSession.Require().TryAddNobody();
            HandleAddResult(result, "Nobody(Guest) — 시스템 기본값으로 파티에 추가됨");
        }

        void AddAgentToParty(RuntimeUserData user, bool isNobody, string okMessage)
        {
            var result = AgentSession.Require().TryAddAgent(user, isNobody);
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

        /// <summary>서버 partyId 발급 후 Station 씬 로드.</summary>
        public void OnEnterStation()
        {
            if (_busy)
                return;

            var session = AgentSession.Require();
            if (!session.HasParty)
            {
                SetIdentityStatus("먼저 에이전트를 1명 이상 등록하세요.");
                ShowStatus("파티가 비어 있습니다");
                return;
            }

            session.SetBayId(bayId);
            var api = PinkSoftApiClient.Instance;
            if (api != null && api.HasToken)
            {
                _busy = true;
                SetIdentityButtonsInteractable(false);
                SetIdentityStatus("서버 파티 등록 중…");
                ShowStatus("partyId 발급 중…");
                StartCoroutine(EnterStationWithParty(session, api));
                return;
            }

            FinishEnterStation(session, offline: true);
        }

        System.Collections.IEnumerator EnterStationWithParty(AgentSession session, PinkSoftApiClient api)
        {
            var members = BuildPartyMemberRequests(session);
            PinkSoftApiClient.PartyResponse? party = null;
            yield return api.CreateParty(session.BayId, members, res => party = res);

            if (party != null && !string.IsNullOrEmpty(party.partyId))
            {
                session.SetServerPartyId(party.partyId);
                FinishEnterStation(session, offline: false);
            }
            else if (allowOfflineClearance)
            {
                ShowStatus("서버 파티 실패 — 로컬 Station 진입");
                FinishEnterStation(session, offline: true);
            }
            else
            {
                _busy = false;
                SetIdentityButtonsInteractable(true);
                SetIdentityStatus("서버 파티 생성에 실패했습니다.");
                ShowStatus("party 생성 실패");
            }
        }

        void FinishEnterStation(AgentSession session, bool offline)
        {
            _busy = false;
            ShowStatus(offline
                ? $"Station 이동 (로컬) — 파티 {session.PartyCount}명"
                : $"Station 이동 — party · {session.PartyCount}명");
            session.EnterStationAndLoadScene();
        }

        static PinkSoftApiClient.PartyMemberRequest[] BuildPartyMemberRequests(AgentSession session)
        {
            var list = new System.Collections.Generic.List<PinkSoftApiClient.PartyMemberRequest>(session.PartyCount);
            for (var i = 0; i < session.PartyCount; i++)
            {
                var slot = session.Party[i];
                var offline = string.IsNullOrEmpty(slot.User.userId)
                              || slot.User.userId.StartsWith("local:", System.StringComparison.Ordinal);
                list.Add(new PinkSoftApiClient.PartyMemberRequest
                {
                    userId = slot.IsNobody || offline ? "" : slot.User.userId,
                    nickname = slot.User.nickname,
                    isNobody = slot.IsNobody || offline
                });
            }

            return list.ToArray();
        }

        public void OnOpenBdsCheck()
        {
            SceneManager.LoadScene(AgentSession.BdsCheckSceneName);
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
