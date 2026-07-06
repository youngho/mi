using System;
using System.Collections.Generic;
using PinkSoft.MissionSDK;

namespace PinkSoft.Core
{
    [Serializable]
    public class ScoreWeightEntry
    {
        public ScoreEventType eventType;
        public int basePoints = 100;
        public float difficultyMultiplier = 1f;
    }

    /// <summary>Core 점수 엔진 — 이벤트 로그 기반 누적 및 별 계산.</summary>
    public sealed class ScoreEngine
    {
        readonly Dictionary<ScoreEventType, int> _weights;
        readonly List<ScoreEventRecord> _log = new();
        int _currentScore;
        int _comboCount;

        public IReadOnlyList<ScoreEventRecord> EventLog => _log;
        public int CurrentScore => _currentScore;

        public event Action<int>? ScoreChanged;

        public ScoreEngine(IEnumerable<ScoreWeightEntry>? weights = null)
        {
            _weights = new Dictionary<ScoreEventType, int>();
            if (weights != null)
            {
                foreach (var w in weights)
                    _weights[w.eventType] = w.basePoints;
            }
            else
            {
                _weights[ScoreEventType.TargetHit] = 100;
                _weights[ScoreEventType.Combo] = 50;
                _weights[ScoreEventType.TimeBonus] = 200;
                _weights[ScoreEventType.ObjectiveComplete] = 500;
                _weights[ScoreEventType.Penalty] = -50;
            }
        }

        public void ProcessEvent(ScoreEventRecord record, int difficultyLevel, EquipmentStats equipment)
        {
            var diffMul = difficultyLevel switch
            {
                1 => 0.8f,
                3 => 1.5f,
                _ => 1f
            };

            var basePts = _weights.GetValueOrDefault(record.eventType, 0);
            if (record.eventType == ScoreEventType.TargetHit)
            {
                _comboCount++;
                if (_comboCount >= 3)
                    ProcessEvent(new ScoreEventRecord { eventType = ScoreEventType.Combo, targetId = record.targetId, timestampMs = record.timestampMs }, difficultyLevel, equipment);
            }
            else if (record.eventType == ScoreEventType.Penalty)
            {
                _comboCount = 0;
            }

            var delta = (int)(basePts * diffMul * equipment.scoreMultiplier);
            _currentScore = Math.Max(0, _currentScore + delta);
            _log.Add(record);
            ScoreChanged?.Invoke(_currentScore);
        }

        public int CalculateStars(int targetScore)
        {
            if (_currentScore >= targetScore * 1.5f) return 3;
            if (_currentScore >= targetScore) return 2;
            if (_currentScore >= targetScore * 0.5f) return 1;
            return 0;
        }

        public MissionResultData BuildResult(int playTimeSeconds, int targetScore)
        {
            return new MissionResultData
            {
                finalScore = _currentScore,
                playTime = playTimeSeconds,
                starsEarned = CalculateStars(targetScore),
                eventLog = new List<ScoreEventRecord>(_log)
            };
        }

        public void Reset()
        {
            _currentScore = 0;
            _comboCount = 0;
            _log.Clear();
        }
    }
}
