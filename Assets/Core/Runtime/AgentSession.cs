using System.Collections.Generic;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core
{
    /// <summary>
    /// 접선(Rendezvous) 파티 세션. 최대 4명까지 클리어런스/Nobody로 등록한 뒤
    /// 별도 버튼으로 Station에 진입한다.
    /// </summary>
    public sealed class AgentSession : MonoBehaviour
    {
        public const int MaxAgents = 4;
        public const string NobodyUserIdPrefix = "nobody";
        public const string NobodyNickname = "Nobody";

        public static AgentSession? Instance { get; private set; }

        readonly List<AgentSlot> _party = new();
        bool _atStation;

        public string? ServerPartyId { get; private set; }
        public string? ActiveRunId { get; private set; }
        public string? ActiveMissionId { get; private set; }
        public string? ActiveMissionTitle { get; private set; }

        public IReadOnlyList<AgentSlot> Party => _party;
        public int PartyCount => _party.Count;
        public bool HasParty => _party.Count > 0;
        public bool CanEnterStation => HasParty && !_atStation;
        public bool IsAtStation => _atStation;
        public bool IsPartyFull => _party.Count >= MaxAgents;

        /// <summary>호환용 — 파티 첫 번째 에이전트.</summary>
        public RuntimeUserData? User => _party.Count > 0 ? _party[0].User : null;

        /// <summary>첫 슬롯이 Nobody인지 (하위 호환).</summary>
        public bool IsNobody => _party.Count > 0 && _party[0].IsNobody;

        public bool UsesSystemDefaults => IsNobody;

        /// <summary>클리어런스 완료 여부 = 파티에 1명 이상 등록됨 (Station 진입과는 별개).</summary>
        public bool IsCleared => HasParty;

        public readonly struct AgentSlot
        {
            public RuntimeUserData User { get; }
            public bool IsNobody { get; }

            public AgentSlot(RuntimeUserData user, bool isNobody)
            {
                User = user;
                IsNobody = isNobody;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public enum AddResult
        {
            Ok,
            PartyFull,
            DuplicateCallsign,
            Invalid
        }

        public AddResult TryAddAgent(RuntimeUserData user, bool isNobody = false)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.nickname))
                return AddResult.Invalid;
            if (IsPartyFull)
                return AddResult.PartyFull;

            var nick = user.nickname.Trim();
            for (var i = 0; i < _party.Count; i++)
            {
                if (string.Equals(_party[i].User.nickname, nick, System.StringComparison.OrdinalIgnoreCase)
                    && !_party[i].IsNobody)
                    return AddResult.DuplicateCallsign;
            }

            // Nobody는 여러 명 가능 — 슬롯별 고유 userId
            if (isNobody)
            {
                var n = CountNobody() + 1;
                user.userId = n == 1 ? NobodyUserIdPrefix : $"{NobodyUserIdPrefix}:{n}";
                user.nickname = NobodyNickname;
                user.equipment = new EquipmentStats();
                user.currentLevel = 1;
            }

            _party.Add(new AgentSlot(user, isNobody));
            return AddResult.Ok;
        }

        public AddResult TryAddNobody() => TryAddAgent(BuildNobodyUser(), isNobody: true);

        public void EnterStation()
        {
            if (!HasParty)
                return;
            _atStation = true;
        }

        public void LeaveStation() => _atStation = false;

        public void SetServerPartyId(string? partyId) => ServerPartyId = partyId;

        public void SetActiveRun(string? runId, string? missionId, string? missionTitle)
        {
            ActiveRunId = runId;
            ActiveMissionId = missionId;
            ActiveMissionTitle = missionTitle;
        }

        public void ClearActiveRun()
        {
            ActiveRunId = null;
            ActiveMissionId = null;
            ActiveMissionTitle = null;
        }

        /// <summary>파티 전체 해제 + Station 이탈 → 접선 화면으로.</summary>
        public void Revoke()
        {
            _party.Clear();
            _atStation = false;
            ServerPartyId = null;
            ClearActiveRun();
        }

        int CountNobody()
        {
            var n = 0;
            for (var i = 0; i < _party.Count; i++)
            {
                if (_party[i].IsNobody)
                    n++;
            }
            return n;
        }

        public static RuntimeUserData BuildNobodyUser() => new()
        {
            userId = NobodyUserIdPrefix,
            nickname = NobodyNickname,
            currentLevel = 1,
            equipment = new EquipmentStats()
        };

        public MissionConfig ResolveMissionConfig(MissionConfig? preferred = null)
        {
            if (UsesSystemDefaults || preferred == null)
                return BuildSystemDefaultConfig();
            return preferred;
        }

        public static MissionConfig BuildSystemDefaultConfig() => new()
        {
            difficultyLevel = 2,
            weatherCondition = "clear",
            timeLimitSeconds = 180,
            targetScore = 5000
        };

        public string BuildPartySummary()
        {
            if (_party.Count == 0)
                return "등록된 에이전트 없음 (최대 4명)";

            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"준비 {_party.Count}/{MaxAgents}");
            for (var i = 0; i < _party.Count; i++)
            {
                var slot = _party[i];
                var tag = slot.IsNobody
                    ? "Nobody (Guest) · 시스템 기본값"
                    : $"{slot.User.nickname}  ·  Lv.{slot.User.currentLevel}";
                lines.AppendLine($"  {i + 1}. {tag}");
            }

            if (!string.IsNullOrEmpty(ServerPartyId))
                lines.AppendLine($"partyId  {ShortId(ServerPartyId)}");
            if (!string.IsNullOrEmpty(ActiveMissionId))
                lines.AppendLine($"mission  {ActiveMissionTitle ?? ActiveMissionId}");
            if (!string.IsNullOrEmpty(ActiveRunId))
                lines.AppendLine($"runId    {ShortId(ActiveRunId)}");

            return lines.ToString().TrimEnd();
        }

        static string ShortId(string id) =>
            id.Length <= 8 ? id : id[..8] + "…";

        public string GetSlotLabel(int index)
        {
            if (index < 0 || index >= MaxAgents)
                return "";
            if (index >= _party.Count)
                return "빈 자리";
            var slot = _party[index];
            if (slot.IsNobody)
                return "Nobody\nGuest · 시스템 기본값 · 준비됨";
            return $"{slot.User.nickname}\nID {slot.User.userId} · Lv.{slot.User.currentLevel} · 준비됨";
        }
    }
}
