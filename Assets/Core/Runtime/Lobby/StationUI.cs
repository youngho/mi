using System.Collections;
using System.Collections.Generic;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// Station 씬 UI. 에이전트/토큰은 <see cref="AgentSession"/> + <see cref="PinkSoftApiClient"/> (DDOL)에서 읽는다.
    /// </summary>
    public sealed class StationUI : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] Text stationAgentText = null!;
        [SerializeField] Button selectMissionButton = null!;
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
        string _hint = "미션을 선택하세요.\n(BDS Check는 우측 상단)";

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

            Bind(selectMissionButton, OnSelectMission);
            Bind(logoutButton, OnLogout);
            Bind(quitButton, OnQuit);
            Bind(bdsCheckButton, OnOpenBdsCheck);

            RefreshPanel();
            StartCoroutine(PrefetchCatalog());
        }

        static void Bind(Button? button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        void RefreshPanel()
        {
            var session = AgentSession.Instance;
            if (stationAgentText == null || session == null)
                return;
            stationAgentText.text = session.BuildPartySummary() + "\n\n" + _hint;
        }

        IEnumerator PrefetchCatalog()
        {
            var api = PinkSoftApiClient.Instance;
            if (api == null || !api.HasToken)
                yield break;

            PinkSoftApiClient.CatalogResponse? catalog = null;
            yield return api.FetchCatalog(null, res => catalog = res);
            if (catalog?.missions == null || catalog.missions.Length == 0)
                yield break;

            _catalog = catalog.missions;
            _hint = BuildCatalogHint(_catalog);
            RefreshPanel();
        }

        static string BuildCatalogHint(PinkSoftApiClient.MissionMeta[] missions)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("미션 카탈로그");
            var n = Mathf.Min(missions.Length, 5);
            for (var i = 0; i < n; i++)
                sb.AppendLine($" · {missions[i].title} ({missions[i].missionId})");
            if (missions.Length > 5)
                sb.AppendLine($" · …외 {missions.Length - 5}개");
            sb.Append("\n[미션 선택] → 첫 미션으로 Run 시작");
            return sb.ToString();
        }

        public void OnSelectMission()
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
            if (selectMissionButton != null)
                selectMissionButton.interactable = false;
            ShowStatus("미션 카탈로그 / Run 준비…");
            StartCoroutine(SelectMissionRoutine(session, api));
        }

        IEnumerator SelectMissionRoutine(AgentSession session, PinkSoftApiClient api)
        {
            if (_catalog.Length == 0)
            {
                PinkSoftApiClient.CatalogResponse? catalog = null;
                yield return api.FetchCatalog(null, res => catalog = res);
                if (catalog?.missions == null || catalog.missions.Length == 0)
                {
                    EndBusy("카탈로그가 비어 있습니다.");
                    yield break;
                }

                _catalog = catalog.missions;
                _hint = BuildCatalogHint(_catalog);
                RefreshPanel();
            }

            var mission = _catalog[0];
            ShowStatus($"Run 시작 — {mission.title}");

            PinkSoftApiClient.RunResponse? run = null;
            yield return api.StartRun(mission.missionId, session.ServerPartyId, session.BayId, res => run = res);
            if (run == null || string.IsNullOrEmpty(run.runId))
            {
                EndBusy("Run 시작 실패");
                yield break;
            }

            session.SetActiveRun(run.runId, mission.missionId, mission.title);
            RefreshPanel();
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
            RefreshPanel();
            EndBusy($"완료 gold+{complete.goldReward} exp+{complete.expGained} rank #{complete.newRank}");
        }

        void EndBusy(string status)
        {
            _busy = false;
            if (selectMissionButton != null)
                selectMissionButton.interactable = true;
            ShowStatus(status);
        }

        public void OnLogout()
        {
            AgentSession.Require().RevokeAndReturnToRendezvous();
        }

        public void OnOpenBdsCheck()
        {
            SceneManager.LoadScene(AgentSession.BdsCheckSceneName);
        }

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
