using PinkSoft.BDS;
using UnityEngine;

namespace PinkSoft.Core.Lobby
{
    /// <summary>
    /// 로비/설정 전용 4점 교정 UI. 미션 씬에는 배치하지 않음.
    /// BdsService가 상주하는 동안 교정 데이터를 유지합니다.
    /// </summary>
    public sealed class LobbyCalibrationUI : MonoBehaviour
    {
        static readonly string[] CornerLabels = { "좌하", "우하", "우상", "좌상" };

        [SerializeField] string saveFileName = "calibration.json";

        CalibrationManager? _manager;
        LidarBulletFilter? _filter;
        bool _listeningForShot;

        void Start()
        {
            if (BdsService.Instance == null)
            {
                Debug.LogWarning("LobbyCalibrationUI: BdsService not found.");
                return;
            }

            _manager = BdsService.Instance.Calibration;
            _filter = BdsService.Instance.Filter;
            if (_filter != null)
                _filter.OnBulletDetected += OnBulletShot;
        }

        void OnDestroy()
        {
            if (_filter != null)
                _filter.OnBulletDetected -= OnBulletShot;
        }

        public void BeginCalibration()
        {
            _manager?.BeginSession();
            _listeningForShot = true;
        }

        void OnBulletShot(BulletDetection det)
        {
            if (!_listeningForShot || _manager == null || _manager.IsComplete)
                return;

            _manager.RegisterCornerShot(det.LidarX, det.LidarY);
            if (_manager.IsComplete)
            {
                _listeningForShot = false;
                BdsService.Instance?.SaveCalibration();
            }
        }

        void OnGUI()
        {
            if (_manager == null)
                return;

            GUILayout.BeginArea(new Rect(10, 40, 360, 220), "BDS 교정 (로비 전용)");
            if (!_manager.IsComplete)
            {
                int idx = Mathf.Min(_manager.CurrentCornerIndex, 3);
                GUILayout.Label($"다음 모서리: {CornerLabels[idx]}");
                if (GUILayout.Button("교정 시작"))
                    BeginCalibration();
            }
            else
            {
                GUILayout.Label("교정 완료 — 미션에서 InputHit 사용");
                if (GUILayout.Button("재시도"))
                    BeginCalibration();
                if (GUILayout.Button("저장"))
                    BdsService.Instance?.SaveCalibration();
            }
            GUILayout.EndArea();
        }
    }
}
