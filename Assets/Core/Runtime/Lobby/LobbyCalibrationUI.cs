using PinkSoft.Core;
using UnityEngine;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 로비에서 BDS Calibration 시스템 모드 진입 버튼.
    /// 4점 교정·발사 테스트는 <see cref="Modes.BdsCalibrationMode"/>에서 수행합니다.
    /// </summary>
    public sealed class LobbyCalibrationUI : MonoBehaviour
    {
        [SerializeField] BdsCalibrationLauncher launcher = null!;

        void Awake()
        {
            if (launcher == null)
                launcher = FindAnyObjectByType<BdsCalibrationLauncher>();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 40, 360, 80), "BDS");
            GUILayout.Label("센서 교정·발사 테스트는 PMS 시스템 모드에서 진행합니다.");
            if (launcher != null && GUILayout.Button("BDS 센서 설정 모드 시작"))
                launcher.LaunchForCurrentUser();
            else
                GUILayout.Label("BdsCalibrationLauncher를 씬에 배치하세요.");
            GUILayout.EndArea();
        }
    }
}
