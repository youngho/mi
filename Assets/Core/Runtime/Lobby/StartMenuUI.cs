using UnityEngine;
using UnityEngine.UI;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// PMS 시작(로비) 화면. 씬에 배치된 Canvas UI를 제어한다.
    /// </summary>
    public sealed class StartMenuUI : MonoBehaviour
    {
        [SerializeField] BdsCalibrationLauncher calibrationLauncher = null!;
        [SerializeField] GameObject rootPanel = null!;
        [SerializeField] Button selectMissionButton = null!;
        [SerializeField] Button calibrationButton = null!;
        [SerializeField] Button quitButton = null!;
        [SerializeField] GameObject statusToast = null!;
        [SerializeField] Text statusText = null!;
        [SerializeField] bool hideCursor;

        void Awake()
        {
            if (calibrationLauncher == null)
                calibrationLauncher = FindAnyObjectByType<BdsCalibrationLauncher>();

            if (hideCursor)
                Cursor.visible = false;

            if (statusToast != null)
                statusToast.SetActive(false);

            WireButtons();
        }

        void WireButtons()
        {
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
        }

        public void OnSelectMission()
        {
            ShowStatus("미션 실행 연동은 다음 단계에서 붙입니다.");
        }

        public void OnOpenCalibration()
        {
            if (calibrationLauncher == null)
            {
                ShowStatus("BdsCalibrationLauncher를 찾을 수 없습니다.");
                return;
            }

            if (rootPanel != null)
                rootPanel.SetActive(false);

            calibrationLauncher.LaunchForCurrentUser();
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
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
