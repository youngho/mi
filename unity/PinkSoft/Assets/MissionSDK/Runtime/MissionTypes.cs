using System;
using System.Collections.Generic;

namespace PinkSoft.MissionSDK
{
    public enum ScoreEventType
    {
        TargetHit,
        Combo,
        TimeBonus,
        ObjectiveComplete,
        Penalty
    }

    public enum MissionErrorCode
    {
        InitializationFailed,
        AssetMissing,
        Timeout,
        InternalError
    }

    [Serializable]
    public class EquipmentStats
    {
        public float accuracyBonus;
        public float scoreMultiplier = 1f;
        public int extraTimeSeconds;
    }

    [Serializable]
    public class RuntimeUserData
    {
        public string userId = "";
        public string nickname = "";
        public int currentLevel = 1;
        public EquipmentStats equipment = new();
    }

    [Serializable]
    public class MissionConfig
    {
        public int difficultyLevel = 2;
        public string weatherCondition = "clear";
        public int timeLimitSeconds = 180;
        public int targetScore = 5000;
    }

    [Serializable]
    public class ScoreEventRecord
    {
        public ScoreEventType eventType;
        public string targetId = "";
        public int timestampMs;
    }

    [Serializable]
    public class MissionResultData
    {
        public int finalScore;
        public int playTime;
        public int starsEarned;
        public List<ScoreEventRecord> eventLog = new();
    }

    [Serializable]
    public class MissionError
    {
        public MissionErrorCode code;
        public string message = "";
    }

    public interface IMissionController
    {
        void InitializeMission(RuntimeUserData userData, MissionContext context);
        void OnPause();
        void OnResume();
        void Shutdown();
        void ReportEvent(ScoreEventType eventType, string targetId);

        event Action<int> OnScoreChanged;
        event Action<bool, MissionResultData> OnMissionEnded;
        event Action<MissionError> OnError;
    }
}
