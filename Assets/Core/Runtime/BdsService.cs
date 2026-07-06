using System;
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
        [SerializeField] string serialPortName = "";
        [SerializeField] int baudRate = 256000;
        [SerializeField] bool autoConnectReader = true;

        readonly CalibrationManager _calibration = new();
        LidarBulletFilter _filter = new();
        LidarHighSpeedReader? _reader;
        BdsInputSource? _bdsInput;
        TouchInputSource? _touchInput;
        DebugInputSource? _debugInput;
        IInputSource? _savedInput;
        bool _calibrationModeActive;

        public CalibrationManager Calibration => _calibration;
        public LidarBulletFilter Filter => _filter;
        public LidarHighSpeedReader? Reader => _reader;
        public IInputSource? ActiveInput { get; private set; }
        public bool IsCalibrationModeActive => _calibrationModeActive;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _bdsInput = gameObject.AddComponent<BdsInputSource>();
            _touchInput = gameObject.AddComponent<TouchInputSource>();
            _debugInput = gameObject.AddComponent<DebugInputSource>();

            _bdsInput.Configure(_filter, _calibration);

            TryLoadCalibration();

            if (autoConnectReader)
                TryConnectReader();

            ActiveInput = SelectInputSource();
            ActiveInput.Enable();
        }

        void Update()
        {
            PumpReaderToFilter();
        }

        void PumpReaderToFilter()
        {
            if (_reader == null)
                return;

            int processed = 0;
            while (processed < 500 && _reader.TryDequeue(out var point))
            {
                _filter.ProcessPoint(point);
                processed++;
            }

            if (processed > 0)
                _filter.EndScanFrame();
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

        /// <summary>PMS BDS Calibration 시스템 모드 진입.</summary>
        public void EnterCalibrationMode()
        {
            if (_calibrationModeActive)
                return;

            _calibrationModeActive = true;
            _savedInput = ActiveInput;
            ActiveInput?.Disable();

            if (!TryConnectReader() && string.IsNullOrWhiteSpace(serialPortName))
                Debug.Log("BdsService: UART 미설정 — 터치/디버그로 교정·테스트 가능");

            ActiveInput = _bdsInput != null && (_reader != null || _bdsInput.IsAvailable)
                ? (IInputSource)_bdsInput
                : (IInputSource?)_touchInput ?? _debugInput!;
            ActiveInput?.Enable();
        }

        /// <summary>BDS Calibration 모드 종료 후 일반 입력 소스로 복귀.</summary>
        public void ExitCalibrationMode()
        {
            if (!_calibrationModeActive)
                return;

            _calibrationModeActive = false;
            ActiveInput?.Disable();
            ActiveInput = _savedInput ?? SelectInputSource();
            _savedInput = null;
            ActiveInput?.Enable();
        }

        public bool TryConnectReader(string? portName = null)
        {
            var port = portName ?? serialPortName;
            if (string.IsNullOrWhiteSpace(port))
                return _reader != null;

            var adapter = CreateSerialPortAdapter(port, baudRate);
            if (adapter == null)
                return false;

            try
            {
                _reader?.Dispose();
                _reader = new LidarHighSpeedReader(adapter, new RplidarA3Parser());
                _reader.Start();
                Debug.Log($"BdsService: LiDAR reader started on {port}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"BdsService: reader connect failed — {ex.Message}");
                _reader = null;
                return false;
            }
        }

        public void DisconnectReader()
        {
            _reader?.Dispose();
            _reader = null;
        }

        public void SetActiveInput(IInputSource source)
        {
            ActiveInput?.Disable();
            ActiveInput = source;
            ActiveInput?.Enable();
        }

        public BdsHardwareStatus GetHardwareStatus()
        {
            return new BdsHardwareStatus
            {
                InputSourceName = ActiveInput?.SourceName ?? "none",
                IsHardwareInput = ReferenceEquals(ActiveInput, _bdsInput),
                IsReaderConnected = _reader != null,
                ReaderState = _reader?.State.ToString() ?? "Stopped",
                BytesReceived = _reader?.BytesReceived ?? 0,
                PointsParsed = _reader?.PointsParsed ?? 0,
                QueueDepth = _reader?.QueueDepth ?? 0,
                IsCalibrated = _calibration.IsComplete,
                CalibrationModeActive = _calibrationModeActive
            };
        }

        static SerialPortLike? CreateSerialPortAdapter(string portName, int baud)
        {
            // Unity에는 System.IO.Ports가 기본 포함되지 않음. LiDAR UART는 tools/bds-capture 또는 추후 네이티브 플러그인.
            return null;
        }

        void OnDestroy()
        {
            ActiveInput?.Disable();
            DisconnectReader();
            if (Instance == this)
                Instance = null;
        }
    }
}
