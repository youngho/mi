using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.BDS.Input
{
    public sealed class BdsInputSource : MonoBehaviour, IInputSource
    {
        LidarBulletFilter? _bulletFilter;
        CalibrationManager? _calibration;

        public string SourceName => "BDS";
        public bool IsAvailable => _bulletFilter != null;

        public event System.Action<InputHit>? OnHit;

        public void Configure(LidarBulletFilter filter, CalibrationManager calibration)
        {
            Disable();
            _bulletFilter = filter;
            _calibration = calibration;
        }

        public void Enable()
        {
            if (_bulletFilter != null)
                _bulletFilter.OnBulletDetected += HandleBullet;
        }

        public void Disable()
        {
            if (_bulletFilter != null)
                _bulletFilter.OnBulletDetected -= HandleBullet;
        }

        void HandleBullet(BulletDetection det)
        {
            float u, v;
            if (_calibration != null && _calibration.IsComplete)
                (u, v) = _calibration.Data.MapLidarToScreen(det.LidarX, det.LidarY);
            else
            {
                u = det.LidarX / 10000f;
                v = det.LidarY / 10000f;
            }

            var screen = new Vector2(u * Screen.width, v * Screen.height);
            OnHit?.Invoke(new InputHit(screen, det.TimestampUs));
        }
    }
}
