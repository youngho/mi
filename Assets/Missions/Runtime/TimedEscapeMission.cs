using System;
using System.Collections.Generic;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Missions
{
    /// <summary>제한 시간 내 탈출구 적중 시 성공.</summary>
    public sealed class TimedEscapeMission : MonoBehaviour, IMissionController
    {
        [SerializeField] float escapeTime = 90f;
        [SerializeField] string exitTargetId = "exit_zone";
        [SerializeField] LayerMask targetLayer;

        readonly MissionInputSubscription _inputSub = new();
        readonly List<ScoreEventRecord> _log = new();

        float _elapsed;
        bool _ended;
        bool _reachedExit;

        public event Action<int>? OnScoreChanged;
        public event Action<bool, MissionResultData>? OnMissionEnded;
        public event Action<MissionError>? OnError;

        public void InitializeMission(RuntimeUserData userData, MissionContext context)
        {
            _elapsed = 0f;
            _ended = false;
            _reachedExit = false;
            _log.Clear();
            escapeTime = context.Config.timeLimitSeconds > 0 ? context.Config.timeLimitSeconds : escapeTime;

            if (context.Input == null)
            {
                OnError?.Invoke(new MissionError
                {
                    code = MissionErrorCode.InitializationFailed,
                    message = "MissionContext.Input is null"
                });
                return;
            }

            _inputSub.Subscribe(context.Input, HandleHit);
        }

        void Update()
        {
            if (_ended) return;
            _elapsed += Time.deltaTime;
            if (_elapsed >= escapeTime)
                Finish(_reachedExit);
        }

        void HandleHit(InputHit hit)
        {
            if (_ended || _reachedExit)
                return;

            if (!MissionHitUtility.TryRaycast(hit, targetLayer, out var rh))
                return;

            if (rh.collider.name != exitTargetId)
                return;

            ReachExit();
        }

        void ReachExit()
        {
            _reachedExit = true;
            ReportEvent(ScoreEventType.ObjectiveComplete, exitTargetId);
            ReportEvent(ScoreEventType.TimeBonus, "time_bonus");
            var bonus = Mathf.Max(0, (int)((escapeTime - _elapsed) * 10));
            OnScoreChanged?.Invoke(1000 + bonus);
            Finish(true);
        }

        public void ReportEvent(ScoreEventType eventType, string targetId)
        {
            _log.Add(new ScoreEventRecord { eventType = eventType, targetId = targetId, timestampMs = (int)(_elapsed * 1000) });
        }

        void Finish(bool success)
        {
            if (_ended) return;
            _ended = true;
            _inputSub.Unsubscribe();
            OnMissionEnded?.Invoke(success, new MissionResultData
            {
                finalScore = success ? 1000 : _log.Count * 50,
                playTime = (int)_elapsed,
                eventLog = new List<ScoreEventRecord>(_log)
            });
        }

        public void OnPause() => enabled = false;
        public void OnResume() => enabled = true;
        public void Shutdown()
        {
            _inputSub.Unsubscribe();
            _ended = true;
        }
    }
}
