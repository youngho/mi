using System;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core
{
    /// <summary>IMissionController + ScoreEngine + MissionInputRouter 브리지.</summary>
    public sealed class MissionSessionController : MonoBehaviour
    {
        [SerializeField] MonoBehaviour missionControllerBehaviour = null!;
        [SerializeField] MissionInputRouter inputRouter = null!;

        IMissionController? _mission;
        ScoreEngine? _scoreEngine;
        RuntimeUserData? _user;
        MissionConfig? _config;
        float _playTime;
        bool _running;

        public ScoreEngine? ScoreEngine => _scoreEngine;
        public MissionInputRouter InputRouter => inputRouter;

        void Awake()
        {
            if (inputRouter == null)
                inputRouter = GetComponent<MissionInputRouter>() ?? gameObject.AddComponent<MissionInputRouter>();

            if (BdsService.Instance != null)
                inputRouter.Configure(BdsService.Instance.ActiveInput!);
        }

        public void StartMission(RuntimeUserData user, MissionConfig config)
        {
            StartMission(missionControllerBehaviour, user, config);
        }

        public void StartMission(MonoBehaviour missionBehaviour, RuntimeUserData user, MissionConfig config)
        {
            missionControllerBehaviour = missionBehaviour;
            _mission = missionControllerBehaviour as IMissionController;
            if (_mission == null)
            {
                Debug.LogError("Mission controller does not implement IMissionController");
                return;
            }

            _user = user;
            _config = config;

            _scoreEngine = new ScoreEngine();
            _scoreEngine.ScoreChanged += HandleScoreChanged;

            _mission.OnMissionEnded += HandleMissionEnded;
            _mission.OnError += err => Debug.LogError($"Mission error: {err.message}");

            var context = new MissionContext
            {
                input = inputRouter,
                config = config
            };

            _mission.InitializeMission(user, context);

            if (BdsService.Instance != null)
                inputRouter.Configure(BdsService.Instance.ActiveInput!);

            inputRouter.Bind();
            _playTime = 0f;
            _running = true;
        }

        void Update()
        {
            if (!_running)
                return;
            _playTime += Time.deltaTime;
        }

        public void ReportToCore(ScoreEventType type, string targetId)
        {
            if (_scoreEngine == null || _mission == null || _user == null || _config == null)
                return;
            var record = new ScoreEventRecord
            {
                eventType = type,
                targetId = targetId,
                timestampMs = (int)(_playTime * 1000f)
            };
            _scoreEngine.ProcessEvent(record, _config.difficultyLevel, _user.equipment);
            _mission.ReportEvent(type, targetId);
        }

        void HandleScoreChanged(int score) => Debug.Log($"Score updated: {score}");

        void HandleMissionEnded(bool success, MissionResultData result)
        {
            _running = false;
            inputRouter.Unbind();

            if (_scoreEngine != null && _config != null)
            {
                result.finalScore = _scoreEngine.CurrentScore;
                result.starsEarned = _scoreEngine.CalculateStars(_config.targetScore);
                result.eventLog = new System.Collections.Generic.List<ScoreEventRecord>(_scoreEngine.EventLog);
            }
            Debug.Log($"Mission ended success={success} score={result.finalScore} stars={result.starsEarned}");
        }

        public void PauseMission()
        {
            inputRouter.SetPaused(true);
            _mission?.OnPause();
        }

        public void ResumeMission()
        {
            inputRouter.SetPaused(false);
            _mission?.OnResume();
        }

        public void ShutdownMission()
        {
            _running = false;
            inputRouter.Unbind();
            _mission?.Shutdown();
        }
    }
}
