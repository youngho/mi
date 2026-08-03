using PinkSoft.Core;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 신원 확인 → Station 흐름을 제어한다. 미션 선택은 클리어런스 이후에만 가능하다.
    /// </summary>
    public sealed class StartMenuUI : MonoBehaviour
    {
        [Header("Flow")]
        [SerializeField] GameObject identityPanel = null!;
        [SerializeField] GameObject stationPanel = null!;

        [Header("Identity")]
        [SerializeField] InputField callsignInput = null!;
        [SerializeField] Button confirmIdentityButton = null!;
        [SerializeField] Text identityStatusText = null!;
        [SerializeField] PinkSoftApiClient apiClient = null!;
        [SerializeField] bool allowOfflineClearance = true;

        [Header("Station")]
        [SerializeField] BdsCalibrationLauncher calibrationLauncher = null!;
        [SerializeField] Button selectMissionButton = null!;
        [SerializeField] Button calibrationButton = null!;
        [SerializeField] Button quitButton = null!;
        [SerializeField] Button logoutButton = null!;
        [SerializeField] Text stationAgentText = null!;
        [SerializeField] GameObject statusToast = null!;
        [SerializeField] Text statusText = null!;
        [SerializeField] bool hideCursor;

        bool _busy;

        void Awake()
        {
            EnsureAgentSession();

            if (calibrationLauncher == null)
                calibrationLauncher = FindAnyObjectByType<BdsCalibrationLauncher>();
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
            if (confirmIdentityButton != null)
            {
                confirmIdentityButton.onClick.RemoveListener(OnConfirmIdentity);
                confirmIdentityButton.onClick.AddListener(OnConfirmIdentity);
            }

            if (selectMissionButton != null)
            {
                selectMissionButton.onClick.RemoveListener(OnSelectMission);
                selectMissionButton.onClick.AddListener(OnSelectMission);
            }

            if (calibrationButton != null)
            {
                calibrationButton.onClick.RemoveListener(OnOpenCalibration);
                calibrationButton.onClick.AddListener(OnOpenCalibration);
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveListener(OnQuit);
                quitButton.onClick.AddListener(OnQuit);
            }

            if (logoutButton != null)
            {
                logoutButton.onClick.RemoveListener(OnLogout);
                logoutButton.onClick.AddListener(OnLogout);
            }
        }

        void RefreshFlow()
        {
            var cleared = AgentSession.Instance != null && AgentSession.Instance.IsCleared;

            if (identityPanel != null)
                identityPanel.SetActive(!cleared);
            if (stationPanel != null)
                stationPanel.SetActive(cleared);

            if (cleared)
                UpdateStationPanel();
            else
                ResetIdentityStatus();
        }

        void ResetIdentityStatus()
        {
            if (identityStatusText != null)
                identityStatusText.text = "콜사인을 입력하고 신원을 확인하세요.";
            if (callsignInput != null && string.IsNullOrEmpty(callsignInput.text))
                callsignInput.text = "";
            _busy = false;
            if (confirmIdentityButton != null)
                confirmIdentityButton.interactable = true;
        }

        public void OnConfirmIdentity()
        {
            if (_busy)
                return;

            var callsign = callsignInput != null ? callsignInput.text.Trim() : "";
            if (string.IsNullOrEmpty(callsign))
            {
                SetIdentityStatus("콜사인이 비어 있습니다.");
                return;
            }

            _busy = true;
            if (confirmIdentityButton != null)
                confirmIdentityButton.interactable = false;
            SetIdentityStatus("신원 확인 중…");

            if (apiClient != null)
            {
                StartCoroutine(apiClient.Login(callsign, ok =>
                {
                    if (ok && apiClient.UserId != null)
                    {
                        CompleteClearance(new RuntimeUserData
                        {
                            userId = apiClient.UserId,
                            nickname = callsign,
                            currentLevel = 1
                        });
                        return;
                    }

                    if (allowOfflineClearance)
                        CompleteClearanceOffline(callsign, "서버 연결 실패 — 오프라인 클리어런스");
                    else
                    {
                        SetIdentityStatus("신원 확인 실패. 서버를 확인하세요.");
                        _busy = false;
                        if (confirmIdentityButton != null)
                            confirmIdentityButton.interactable = true;
                    }
                }));
                return;
            }

            if (allowOfflineClearance)
                CompleteClearanceOffline(callsign, "로컬 클리어런스");
            else
            {
                SetIdentityStatus("API 클라이언트가 없습니다.");
                _busy = false;
                if (confirmIdentityButton != null)
                    confirmIdentityButton.interactable = true;
            }
        }

        void CompleteClearanceOffline(string callsign, string note)
        {
            CompleteClearance(new RuntimeUserData
            {
                userId = $"local:{callsign}",
                nickname = callsign,
                currentLevel = 1
            });
            ShowStatus(note);
        }

        void CompleteClearance(RuntimeUserData user)
        {
            AgentSession.Instance!.ClearIdentity(user);
            _busy = false;
            if (confirmIdentityButton != null)
                confirmIdentityButton.interactable = true;
            RefreshFlow();
            ShowStatus($"에이전트 {user.nickname} — 클리어런스 승인");
        }

        void UpdateStationPanel()
        {
            var user = AgentSession.Instance?.User;
            if (stationAgentText == null || user == null)
                return;

            stationAgentText.text =
                $"에이전트: {user.nickname}\n" +
                $"ID: {user.userId}\n" +
                $"레벨: {user.currentLevel}\n\n" +
                "스테이션 준비 완료.\n미션을 선택하거나 센서를 설정하세요.";
        }

        public void OnSelectMission()
        {
            if (!EnsureCleared())
                return;
            ShowStatus("미션 실행 연동은 다음 단계에서 붙입니다.");
        }

        public void OnOpenCalibration()
        {
            if (!EnsureCleared())
                return;

            if (calibrationLauncher == null)
            {
                ShowStatus("BdsCalibrationLauncher를 찾을 수 없습니다.");
                return;
            }

            if (stationPanel != null)
                stationPanel.SetActive(false);
            if (identityPanel != null)
                identityPanel.SetActive(false);

            calibrationLauncher.LaunchForCurrentUser();
        }

        public void OnLogout()
        {
            AgentSession.Instance?.Revoke();
            RefreshFlow();
            ShowStatus("클리어런스가 해제되었습니다.");
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        bool EnsureCleared()
        {
            if (AgentSession.Instance != null && AgentSession.Instance.IsCleared)
                return true;

            RefreshFlow();
            ShowStatus("먼저 신원을 확인하세요.");
            return false;
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
