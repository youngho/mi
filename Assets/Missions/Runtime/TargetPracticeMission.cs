using System;
using System.Collections.Generic;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Missions
{
    /// <summary>타겟 연습 — Core 주입 InputHit로 타겟 적중.</summary>
    public sealed class TargetPracticeMission : MonoBehaviour, IMissionController
    {
        [SerializeField] int targetsToClear = 5;
        [SerializeField] float timeLimit = 120f;
        [SerializeField] LayerMask targetLayer;

        readonly MissionInputSubscription _inputSub = new();
        readonly List<ScoreEventRecord> _log = new();

        int _cleared;
        float _elapsed;
        bool _ended;

        public event Action<int>? OnScoreChanged;
        public event Action<bool, MissionResultData>? OnMissionEnded;
        public event Action<MissionError>? OnError;

        public void InitializeMission(RuntimeUserData userData, MissionContext context)
        {
            _cleared = 0;
            _elapsed = 0f;
            _ended = false;
            _log.Clear();
            timeLimit = context.Config.timeLimitSeconds > 0 ? context.Config.timeLimitSeconds : timeLimit;

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
            if (_elapsed >= timeLimit)
                Finish(false);
        }

        void HandleHit(InputHit hit)
        {
            if (_ended)
                return;

            if (!MissionHitUtility.TryRaycast(hit, targetLayer, out var rh))
                return;

            RegisterHit(rh.collider.name);
            rh.collider.enabled = false;
        }

        void RegisterHit(string targetId)
        {
            if (_ended) return;
            ReportEvent(ScoreEventType.TargetHit, targetId);
            _cleared++;
            OnScoreChanged?.Invoke(_cleared * 100);
            if (_cleared >= targetsToClear)
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
                finalScore = _cleared * 100,
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
