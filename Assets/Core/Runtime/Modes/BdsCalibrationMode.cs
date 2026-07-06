using System;
using System.Collections.Generic;
using PinkSoft.BDS;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core.Modes
{
    /// <summary>
    /// PMS 시스템 모드: 사용자별 BDS 4점 교정 + 발사 테스트.
    /// 로비에서 진입하며 IMissionController 계약을 따릅니다.
    /// </summary>
    public sealed class BdsCalibrationMode : MonoBehaviour, IMissionController
    {
        public const string ModeId = "system_bds_calibration";

        enum Phase
        {
            Intro,
            Corners,
            ShotTest,
            Summary
        }

        static readonly string[] CornerLabels = { "좌하", "우하", "우상", "좌상" };

        [SerializeField] int testShotsGoal = 10;
        [SerializeField] float shotTestDurationSec = 120f;
        [SerializeField] bool showHitMarkers = true;

        Phase _phase = Phase.Intro;
        CalibrationManager? _calibration;
        LidarBulletFilter? _filter;
        readonly MissionInputSubscription _inputSub = new();
        readonly BulletFilterStatistics _stats = new();
        readonly List<Vector2> _testHitMarkers = new();
        readonly List<ScoreEventRecord> _eventLog = new();

        bool _listeningCorners;
        bool _ended;
        float _testElapsed;
        int _mappedHits;
        RuntimeUserData? _user;
        BdsService? _bds;

        public event Action<int>? OnScoreChanged;
        public event Action<bool, MissionResultData>? OnMissionEnded;
        public event Action<MissionError>? OnError;

        public void InitializeMission(RuntimeUserData userData, MissionContext context)
        {
            _user = userData;
            _ended = false;
            _phase = Phase.Intro;
            _listeningCorners = false;
            _testElapsed = 0f;
            _mappedHits = 0;
            _eventLog.Clear();
            _testHitMarkers.Clear();
            _stats.Reset();

            _bds = BdsService.Instance;
            if (_bds == null)
            {
                OnError?.Invoke(new MissionError
                {
                    code = MissionErrorCode.InitializationFailed,
                    message = "BdsService not found — Boot 씬에 BdsService를 배치하세요"
                });
                return;
            }

            _bds.EnterCalibrationMode();
            _calibration = _bds.Calibration;
            _filter = _bds.Filter;

            if (_filter != null)
                _filter.OnBulletDetected += OnBulletForCorner;

            if (context.Input == null)
            {
                OnError?.Invoke(new MissionError
                {
                    code = MissionErrorCode.InitializationFailed,
                    message = "MissionContext.Input is null"
                });
                return;
            }

            _inputSub.Subscribe(context.Input, OnInputHit);
        }

        void Update()
        {
            if (_ended || _phase != Phase.ShotTest)
                return;

            _testElapsed += Time.deltaTime;
            if (_mappedHits >= testShotsGoal || _testElapsed >= shotTestDurationSec)
                FinishShotTest();
        }

        void OnGUI()
        {
            if (_ended || _calibration == null)
                return;

            var status = _bds?.GetHardwareStatus();
            GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20), "BDS 센서 설정");

            DrawStatusHeader(status);
            GUILayout.Space(8);

            switch (_phase)
            {
                case Phase.Intro:
                    DrawIntroPhase();
                    break;
                case Phase.Corners:
                    DrawCornersPhase();
                    break;
                case Phase.ShotTest:
                    DrawShotTestPhase();
                    break;
                case Phase.Summary:
                    DrawSummaryPhase();
                    break;
            }

            GUILayout.EndArea();

            if (showHitMarkers)
                DrawHitMarkers();
        }

        void DrawStatusHeader(BdsHardwareStatus? status)
        {
            if (status == null)
            {
                GUILayout.Label("BdsService 없음");
                return;
            }

            GUILayout.Label($"입력: {status.Value.InputSourceName} | LiDAR: {status.Value.ReaderState}");
            GUILayout.Label($"교정: {(status.Value.IsCalibrated ? "완료" : "미완료")} | 큐: {status.Value.QueueDepth}");
        }

        void DrawIntroPhase()
        {
            GUILayout.Label("프로젝터 화면 4모서리를 순서대로 쏴 교정한 뒤,");
            GUILayout.Label("발사 테스트로 센서를 검증합니다.");
            GUILayout.Space(4);
            if (GUILayout.Button("교정 시작"))
                BeginCornerCalibration();
            if (_calibration!.IsComplete && GUILayout.Button("교정 건너뛰기 → 테스트"))
                BeginShotTest();
        }

        void DrawCornersPhase()
        {
            int idx = Mathf.Min(_calibration!.CurrentCornerIndex, 3);
            GUILayout.Label($"다음 모서리: {CornerLabels[idx]}");
            GUILayout.Label("해당 모서리를 조준해 발사하세요.");
            if (GUILayout.Button("처음부터"))
                BeginCornerCalibration();
            if (_calibration.IsComplete && GUILayout.Button("발사 테스트 시작"))
                BeginShotTest();
        }

        void DrawShotTestPhase()
        {
            GUILayout.Label($"적중 {_mappedHits}/{testShotsGoal} | 남은 시간 {RemainingTestSec():F0}s");
            GUILayout.Label(_stats.BuildReport().ToString());
            if (GUILayout.Button("발사 기록 (수동)"))
                _stats.RegisterShotFired();
            if (GUILayout.Button("테스트 종료"))
                FinishShotTest();
        }

        void DrawSummaryPhase()
        {
            var report = _stats.BuildReport();
            GUILayout.Label($"검출률: {report.DetectionRate:P1} (목표 ≥70%)");
            GUILayout.Label($"이중검출: {report.DoubleDetectionRate:P1}");
            GUILayout.Label($"평균 지연: {report.AverageLatencyMs:F1}ms");
            GUILayout.Space(4);
            if (_calibration!.IsComplete && GUILayout.Button("교정 저장"))
                _bds?.SaveCalibration();
            if (GUILayout.Button("완료"))
                EndMission(true);
            if (GUILayout.Button("교정 다시"))
                BeginCornerCalibration();
        }

        void DrawHitMarkers()
        {
            foreach (var pos in _testHitMarkers)
            {
                var rect = new Rect(pos.x - 8, Screen.height - pos.y - 8, 16, 16);
                GUI.Box(rect, "");
            }
        }

        float RemainingTestSec() => Mathf.Max(0f, shotTestDurationSec - _testElapsed);

        public void BeginCornerCalibration()
        {
            _phase = Phase.Corners;
            _calibration?.BeginSession();
            _listeningCorners = true;
        }

        void BeginShotTest()
        {
            if (_calibration == null || !_calibration.IsComplete)
            {
                Debug.LogWarning("BdsCalibrationMode: 교정을 먼저 완료하세요.");
                return;
            }

            _phase = Phase.ShotTest;
            _listeningCorners = false;
            _testElapsed = 0f;
            _mappedHits = 0;
            _testHitMarkers.Clear();
            _stats.Reset();
        }

        void FinishShotTest()
        {
            _phase = Phase.Summary;
            _listeningCorners = false;
        }

        void OnBulletForCorner(BulletDetection det)
        {
            if (!_listeningCorners || _calibration == null || _calibration.IsComplete)
                return;

            _calibration.RegisterCornerShot(det.LidarX, det.LidarY);
            if (_calibration.IsComplete)
            {
                _listeningCorners = false;
                _bds?.SaveCalibration();
            }
        }

        void OnInputHit(InputHit hit)
        {
            if (_phase == Phase.Corners && _listeningCorners && _calibration != null && !_calibration.IsComplete)
            {
                var norm = new Vector2(hit.ScreenPosition.x / Screen.width, hit.ScreenPosition.y / Screen.height);
                _calibration.RegisterCornerFromScreen(norm.x, norm.y);
                if (_calibration.IsComplete)
                {
                    _listeningCorners = false;
                    _bds?.SaveCalibration();
                }
                return;
            }

            if (_phase != Phase.ShotTest)
                return;

            _mappedHits++;
            _testHitMarkers.Add(hit.ScreenPosition);
            _stats.RegisterDetection(0, hit.TimestampUs, 0);
            ReportEvent(ScoreEventType.TargetHit, $"test_{_mappedHits}");
            OnScoreChanged?.Invoke(_mappedHits);

            if (_mappedHits >= testShotsGoal)
                FinishShotTest();
        }

        void EndMission(bool success)
        {
            if (_ended)
                return;
            _ended = true;
            _listeningCorners = false;
            _inputSub.Unsubscribe();

            if (_filter != null)
                _filter.OnBulletDetected -= OnBulletForCorner;

            _bds?.ExitCalibrationMode();

            var report = _stats.BuildReport();
            OnMissionEnded?.Invoke(success, new MissionResultData
            {
                finalScore = _mappedHits,
                playTime = (int)_testElapsed,
                starsEarned = report.DetectionRate >= 0.7f ? 3 : report.DetectionRate >= 0.5f ? 2 : 1,
                eventLog = new List<ScoreEventRecord>(_eventLog)
            });
        }

        public void ReportEvent(ScoreEventType eventType, string targetId)
        {
            _eventLog.Add(new ScoreEventRecord
            {
                eventType = eventType,
                targetId = targetId,
                timestampMs = (int)(_testElapsed * 1000f)
            });
        }

        public void OnPause() => enabled = false;
        public void OnResume() => enabled = true;

        public void Shutdown()
        {
            _ended = true;
            _listeningCorners = false;
            _inputSub.Unsubscribe();
            if (_filter != null)
                _filter.OnBulletDetected -= OnBulletForCorner;
            _bds?.ExitCalibrationMode();
        }
    }
}
