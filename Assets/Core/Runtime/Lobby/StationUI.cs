using System.Collections;
using System.Collections.Generic;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// Station 씬 UI. 미션은 앨범형(가운데 추천 + 하단 목록)으로 고른다.
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

        [Header("Options")]
        [SerializeField] bool submitTestResultOnSelect = true;
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
            if (stationAgentText == null || session == null)
                return;
            stationAgentText.text = session.BuildPartySummary() + "\n\n위 목록에서 미션을 고르세요.";
        }

        IEnumerator PrefetchCatalog()
        {
            var api = PinkSoftApiClient.Instance;
            if (api == null || !api.HasToken)
            {
                ShowStatus("온라인 Clearance 후 카탈로그를 불러옵니다");
                yield break;
            }

            ShowStatus("미션 카탈로그 로딩…");
            PinkSoftApiClient.CatalogResponse? catalog = null;
            yield return api.FetchCatalog(null, res => catalog = res);
            if (catalog?.missions == null || catalog.missions.Length == 0)
            {
                ShowStatus("카탈로그가 비어 있습니다");
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
            if (api == null || !api.HasToken)
            {
                ShowStatus("API 토큰 없음 — Rendezvous에서 온라인 Clearance 필요");
                return;
            }

            _busy = true;
            ShowStatus($"투입 준비 — {mission.title}");
            StartCoroutine(DeployRoutine(session, api, mission));
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
