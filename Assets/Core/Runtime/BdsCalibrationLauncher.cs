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

            sessionController.StartMission(calibrationMode, ResolveUser(), BuildCalibrationConfig());
        }

        public static RuntimeUserData ResolveUser()
        {
            if (AgentSession.Instance != null && AgentSession.Instance.IsCleared && AgentSession.Instance.User != null)
                return AgentSession.Instance.User;

            // 세션 없이 교정만 열 때 — Nobody와 동일하게 시스템 기본 프로필
            return AgentSession.BuildNobodyUser();
        }

        public static RuntimeUserData BuildGuestUser() => AgentSession.BuildNobodyUser();

        public static MissionConfig BuildCalibrationConfig()
        {
            // 교정 모드는 항상 시스템 기본(교정용) 설정
            return new MissionConfig
            {
                difficultyLevel = 1,
                weatherCondition = "clear",
                timeLimitSeconds = 600,
                targetScore = 0
            };
        }
    }
}
