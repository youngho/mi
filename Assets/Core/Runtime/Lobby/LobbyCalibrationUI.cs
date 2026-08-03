using PinkSoft.Core;
using UnityEngine;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 레거시 IMGUI 진입점. 시작 화면은 <see cref="StartMenuUI"/>를 사용하세요.
    /// </summary>
    [System.Obsolete("StartMenuUI로 대체됨")]
    public sealed class LobbyCalibrationUI : MonoBehaviour
    {
        [SerializeField] BdsCalibrationLauncher launcher = null!;

        void Awake()
        {
            if (GetComponent<StartMenuUI>() != null)
            {
                enabled = false;
                return;
            }

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
