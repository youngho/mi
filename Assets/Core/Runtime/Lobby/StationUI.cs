using System.Collections;
using System.Collections.Generic;
using PinkSoft.Core;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// Station 씬 UI. 전술 태블릿: 상단 파티 슬롯, 가운데 미션 카드, 하단 상세·투입.
    /// </summary>
    public sealed class StationUI : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] Text stationAgentText = null!;
        [SerializeField] MissionAlbumView missionAlbum = null!;
        [SerializeField] Button logoutButton = null!;
        [SerializeField] Button quitButton = null!;
        [SerializeField] Button bdsCheckButton = null!;
        [SerializeField] GameObject statusToast = null!;
        [SerializeField] Text statusText = null!;

        [Header("Party Bar")]
        [SerializeField] GameObject[] partyMemberChips = System.Array.Empty<GameObject>();
        [SerializeField] Text[] partyMemberLabels = System.Array.Empty<Text>();
        [SerializeField] Image[] partyMemberChipImages = System.Array.Empty<Image>();
        [SerializeField] Button editPartyButton = null!;
        [SerializeField] Color chipMemberColor = Color.white;
        [SerializeField] Color chipNobodyColor = new Color(0.82f, 0.84f, 0.82f, 1f);
        [SerializeField] Color chipEmptyColor = new Color(0.72f, 0.74f, 0.74f, 1f);

        [Header("Options")]
        [SerializeField] bool submitTestResultOnSelect = true;
        [SerializeField] bool allowOfflinePlay = true;
        [SerializeField] bool hideCursor;

        bool _busy;
        PinkSoftApiClient.MissionMeta[] _catalog = System.Array.Empty<PinkSoftApiClient.MissionMeta>();

        void Awake()
        {
            var session = AgentSession.Ensure();
            if (!session.HasParty)
            {
                Debug.LogWarning("[Station] 파티 없음 — Rendezvous로 복귀");
                SceneManager.LoadScene(AgentSession.RendezvousSceneName);
                return;
            }

            if (!session.IsAtStation)
                session.EnterStation();

            if (hideCursor)
                Cursor.visible = false;

            if (statusToast != null)
                statusToast.SetActive(false);

            Bind(logoutButton, OnLogout);
            Bind(editPartyButton, OnEditParty);
            Bind(quitButton, OnQuit);
            Bind(bdsCheckButton, OnOpenBdsCheck);

            if (missionAlbum != null)
                missionAlbum.BindDeploy(OnDeployMission);

            RefreshPartyPanel();
            StartCoroutine(PrefetchCatalog());
        }

        static void Bind(Button? button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        void RefreshPartyPanel()
        {
            var session = AgentSession.Instance;
            if (session == null)
                return;

            var count = partyMemberChips.Length;
            for (var i = 0; i < count; i++)
            {
                var occupied = i < session.PartyCount;
                if (partyMemberChips[i] != null)
                    partyMemberChips[i].SetActive(true);
                if (!occupied)
                {
                    if (i < partyMemberLabels.Length && partyMemberLabels[i] != null)
                        partyMemberLabels[i].text = $"Squad Mate {i + 1}";
                    if (i < partyMemberChipImages.Length && partyMemberChipImages[i] != null)
                        partyMemberChipImages[i].color = chipEmptyColor;
                    continue;
                }

                var slot = session.Party[i];
                var nick = slot.IsNobody ? AgentSession.NobodyNickname : slot.User.nickname;
                if (i < partyMemberLabels.Length && partyMemberLabels[i] != null)
                    partyMemberLabels[i].text = nick;
                if (i < partyMemberChipImages.Length && partyMemberChipImages[i] != null)
                    partyMemberChipImages[i].color = slot.IsNobody ? chipNobodyColor : chipMemberColor;
            }

            if (stationAgentText != null)
                stationAgentText.text = $"파티 {session.PartyCount}/{AgentSession.MaxAgents}";
        }

        IEnumerator PrefetchCatalog()
        {
            var api = PinkSoftApiClient.Instance;
            if (api == null)
            {
                ShowStatus("API 클라이언트가 없습니다");
                yield break;
            }

            // 카탈로그는 공개 GET. Nobody/오프라인도 목록은 볼 수 있다.
            ShowStatus("미션 카탈로그 로딩…");
            PinkSoftApiClient.CatalogResponse? catalog = null;
            yield return api.FetchCatalog(null, res => catalog = res);
            if (catalog?.missions == null || catalog.missions.Length == 0)
            {
                ShowStatus("카탈로그를 불러오지 못했습니다");
                yield break;
            }

            _catalog = catalog.missions;
            var rec = 0;
            for (var i = 0; i < _catalog.Length; i++)
            {
                if (_catalog[i].missionId == "training-range-v1")
                {
                    rec = i;
                    break;
                }
            }

            missionAlbum?.SetMissions(_catalog, recommendedIndex: rec);
            RefreshPartyPanel();
            ShowStatus($"미션 {_catalog.Length}개 · 추천: {_catalog[rec].title}");
        }

        void OnDeployMission(PinkSoftApiClient.MissionMeta mission)
        {
            if (_busy)
                return;

            var session = AgentSession.Require();
            var api = PinkSoftApiClient.Instance;
            if (api == null)
            {
                ShowStatus("API 클라이언트가 없습니다");
                return;
            }

            if (!api.HasToken)
            {
                if (!allowOfflinePlay)
                {
                    ShowStatus("API 토큰 없음 — 온라인 Clearance 필요");
                    return;
                }

                StartLocalDeploy(session, mission);
                return;
            }

            _busy = true;
            ShowStatus($"투입 준비 — {mission.title}");
            StartCoroutine(DeployRoutine(session, api, mission));
        }

        void StartLocalDeploy(AgentSession session, PinkSoftApiClient.MissionMeta mission)
        {
            session.SetActiveRun($"local:{mission.missionId}", mission.missionId, mission.title);
            RefreshPartyPanel();
            ShowStatus($"게스트 런 (로컬) — {mission.title}");
        }

        IEnumerator DeployRoutine(AgentSession session, PinkSoftApiClient api, PinkSoftApiClient.MissionMeta mission)
        {
            PinkSoftApiClient.RunResponse? run = null;
            yield return api.StartRun(mission.missionId, session.ServerPartyId, session.BayId, res => run = res);
            if (run == null || string.IsNullOrEmpty(run.runId))
            {
                EndBusy("Run 시작 실패");
                yield break;
            }

            session.SetActiveRun(run.runId, mission.missionId, mission.title);
            RefreshPartyPanel();
            ShowStatus($"run {ShortId(run.runId)} 시작됨");

            if (!submitTestResultOnSelect)
            {
                EndBusy($"런 준비 완료 — {mission.title}");
                yield break;
            }

            ShowStatus("테스트 결과 제출 중…");
            var result = new MissionResultData
            {
                finalScore = 100,
                playTime = 10,
                starsEarned = 1,
                eventLog = new List<ScoreEventRecord>()
            };

            PinkSoftApiClient.CompleteResponse? complete = null;
            yield return api.CompleteMission(result, mission.missionId, run.runId, res => complete = res);
            if (complete == null)
            {
                EndBusy("결과 제출 실패");
                yield break;
            }

            session.ClearActiveRun();
            RefreshPartyPanel();
            EndBusy($"완료 gold+{complete.goldReward} exp+{complete.expGained} rank #{complete.newRank}");
        }

        void EndBusy(string status)
        {
            _busy = false;
            ShowStatus(status);
        }

        public void OnLogout() => AgentSession.Require().RevokeAndReturnToRendezvous();

        /// <summary>파티 수정 — 클리어런스는 유지한 채 이전(접선) 씬으로 복귀.</summary>
        public void OnEditParty() => AgentSession.Require().ReturnToRendezvous(keepParty: true);

        public void OnOpenBdsCheck() => SceneManager.LoadScene(AgentSession.BdsCheckSceneName);

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static string ShortId(string? id)
        {
            if (string.IsNullOrEmpty(id))
                return "—";
            return id.Length <= 8 ? id : id[..8] + "…";
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
