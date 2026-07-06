using PinkSoft.BDS;
using PinkSoft.BDS.Input;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core
{
    /// <summary>
    /// BDS 하위시스템 lifecycle 싱글톤. PMS Core에 상주하며 미션 전환 시에도 유지.
    /// </summary>
    public sealed class BdsService : MonoBehaviour
    {
        public static BdsService? Instance { get; private set; }

        [SerializeField] bool preferBdsHardware = true;
        [SerializeField] bool useDebugInputInEditor = true;
        [SerializeField] string calibrationSavePath = "calibration.json";

        readonly CalibrationManager _calibration = new();
        LidarBulletFilter? _filter;
        BdsInputSource? _bdsInput;
        TouchInputSource? _touchInput;
        DebugInputSource? _debugInput;

        public CalibrationManager Calibration => _calibration;
        public LidarBulletFilter? Filter => _filter;
        public IInputSource? ActiveInput { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _filter = gameObject.AddComponent<LidarBulletFilter>();
            _bdsInput = gameObject.AddComponent<BdsInputSource>();
            _touchInput = gameObject.AddComponent<TouchInputSource>();
            _debugInput = gameObject.AddComponent<DebugInputSource>();

            _bdsInput.Configure(_filter, _calibration);

            TryLoadCalibration();

            ActiveInput = SelectInputSource();
            ActiveInput.Enable();
        }

        IInputSource SelectInputSource()
        {
#if UNITY_EDITOR
            if (useDebugInputInEditor && _debugInput != null)
                return _debugInput;
#endif
            if (preferBdsHardware && _bdsInput != null && _bdsInput.IsAvailable)
                return _bdsInput;
            return _touchInput!;
        }

        public void TryLoadCalibration()
        {
            try
            {
                var path = System.IO.Path.Combine(Application.persistentDataPath, calibrationSavePath);
                if (System.IO.File.Exists(path))
                    _calibration.LoadFromFile(path);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Calibration load skipped: {ex.Message}");
            }
        }

        public void SaveCalibration()
        {
            if (!_calibration.IsComplete)
                return;
            var path = System.IO.Path.Combine(Application.persistentDataPath, calibrationSavePath);
            _calibration.SaveToFile(path);
        }

        void OnDestroy()
        {
            ActiveInput?.Disable();
            if (Instance == this)
                Instance = null;
        }
    }
}
