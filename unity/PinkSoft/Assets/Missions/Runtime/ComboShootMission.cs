using System;
using System.Collections.Generic;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Missions
{
    /// <summary>연속 콤보 — Core 주입 InputHit로 타겟 적중.</summary>
    public sealed class ComboShootMission : MonoBehaviour, IMissionController
    {
        [SerializeField] int requiredCombo = 10;
        [SerializeField] float comboWindowSeconds = 2f;
        [SerializeField] LayerMask targetLayer;

        readonly MissionInputSubscription _inputSub = new();
        readonly List<ScoreEventRecord> _log = new();

        int _combo;
        int _maxCombo;
        float _lastHitTime;
        float _elapsed;
        bool _ended;

        public event Action<int>? OnScoreChanged;
        public event Action<bool, MissionResultData>? OnMissionEnded;
        public event Action<MissionError>? OnError;

        public void InitializeMission(RuntimeUserData userData, MissionContext context)
        {
            _combo = 0;
            _maxCombo = 0;
            _elapsed = 0f;
            _ended = false;
            _log.Clear();

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
            if (_combo > 0 && _elapsed - _lastHitTime > comboWindowSeconds)
                _combo = 0;
        }

        void HandleHit(InputHit hit)
        {
            if (_ended)
                return;

            if (!MissionHitUtility.TryRaycast(hit, targetLayer, out var rh))
                return;

            RegisterHit(rh.collider.name);
        }

        void RegisterHit(string targetId)
        {
            if (_ended) return;
            _lastHitTime = _elapsed;
            _combo++;
            if (_combo > _maxCombo)
                _maxCombo = _combo;

            ReportEvent(ScoreEventType.TargetHit, targetId);
            if (_combo >= 3)
                ReportEvent(ScoreEventType.Combo, $"combo_{_combo}");

            OnScoreChanged?.Invoke(_combo * 100);

            if (_maxCombo >= requiredCombo)
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
                finalScore = _maxCombo * 100,
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
