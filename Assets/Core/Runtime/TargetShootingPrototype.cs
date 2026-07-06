using System;
using System.Collections.Generic;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core
{
    /// <summary>단일 미션 프로토타입: Core 주입 InputHit → Raycast → ReportEvent.</summary>
    public sealed class TargetShootingPrototype : MonoBehaviour, IMissionController
    {
        [SerializeField] LayerMask targetLayer;
        [SerializeField] int maxTargets = 5;
        [SerializeField] float missionDuration = 60f;

        readonly MissionInputSubscription _inputSub = new();
        readonly List<ScoreEventRecord> _eventLog = new();

        MissionConfig? _config;
        float _elapsed;
        int _hits;
        bool _ended;

        public event System.Action<int>? OnScoreChanged;
        public event System.Action<bool, MissionResultData>? OnMissionEnded;
        public event System.Action<MissionError>? OnError;

        public void InitializeMission(RuntimeUserData userData, MissionContext context)
        {
            _config = context.Config;
            _elapsed = 0f;
            _hits = 0;
            _ended = false;
            _eventLog.Clear();

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
            if (_ended || _config == null)
                return;

            _elapsed += Time.deltaTime;
            var limit = _config.timeLimitSeconds > 0 ? _config.timeLimitSeconds : missionDuration;
            if (_elapsed >= limit)
                EndMission(_hits >= 1);
        }

        void HandleHit(InputHit hit)
        {
            if (_ended)
                return;

            if (!MissionHitUtility.TryRaycast(hit, targetLayer, out var rh))
                return;

            var targetId = rh.collider.name;
            _hits++;
            ReportEvent(ScoreEventType.TargetHit, targetId);
            OnScoreChanged?.Invoke(_hits * 100);

            rh.collider.enabled = false;
            if (_hits >= maxTargets)
                EndMission(true);
        }

        public void ReportEvent(ScoreEventType eventType, string targetId)
        {
            _eventLog.Add(new ScoreEventRecord
            {
                eventType = eventType,
                targetId = targetId,
                timestampMs = (int)(_elapsed * 1000f)
            });
        }

        void EndMission(bool success)
        {
            if (_ended)
                return;
            _ended = true;
            _inputSub.Unsubscribe();

            OnMissionEnded?.Invoke(success, new MissionResultData
            {
                finalScore = _hits * 100,
                playTime = (int)_elapsed,
                starsEarned = 0,
                eventLog = new List<ScoreEventRecord>(_eventLog)
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
