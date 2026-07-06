using PinkSoft.Core.Modes;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core
{
    /// <summary>로비에서 BDS Calibration 시스템 모드로 진입하는 런처.</summary>
    public sealed class BdsCalibrationLauncher : MonoBehaviour
    {
        [SerializeField] BdsCalibrationMode calibrationMode = null!;
        [SerializeField] MissionSessionController sessionController = null!;

        void Awake()
        {
            if (calibrationMode == null)
                calibrationMode = GetComponentInChildren<BdsCalibrationMode>(true);
            if (sessionController == null)
                sessionController = FindAnyObjectByType<MissionSessionController>();
        }

        public void LaunchForCurrentUser()
        {
            if (sessionController == null || calibrationMode == null)
            {
                Debug.LogError("BdsCalibrationLauncher: sessionController or calibrationMode missing");
                return;
            }

            sessionController.StartMission(calibrationMode, BuildGuestUser(), BuildCalibrationConfig());
        }

        public static RuntimeUserData BuildGuestUser() => new()
        {
            userId = SystemInfo.deviceUniqueIdentifier,
            nickname = "센서 테스트",
            currentLevel = 1
        };

        public static MissionConfig BuildCalibrationConfig() => new()
        {
            difficultyLevel = 1,
            timeLimitSeconds = 600,
            targetScore = 0
        };

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(Screen.width - 220, 10, 210, 60));
            if (GUILayout.Button("BDS 센서 설정"))
                LaunchForCurrentUser();
            GUILayout.EndArea();
        }
    }
}
