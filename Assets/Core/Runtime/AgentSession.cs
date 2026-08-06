using System.Collections.Generic;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinkSoft.Core
{
    /// <summary>
    /// 접선 파티·런 상태를 씬 간에 유지하는 싱글톤 (DontDestroyOnLoad).
    /// 미션/Station 등 어느 씬에서 <see cref="Require"/> 또는 <see cref="Instance"/>로 접근한다.
    /// </summary>
    public sealed class AgentSession : MonoBehaviour
    {
        public const int MaxAgents = 4;
        public const string NobodyUserIdPrefix = "nobody";
        public const string NobodyNickname = "Nobody";

        public const string BootSceneName = "Boot";
        public const string RendezvousSceneName = "Rendezvous";
        public const string StationSceneName = "Station";
        public const string BdsCheckSceneName = "BdsCheck";

        public static AgentSession? Instance { get; private set; }

        readonly List<AgentSlot> _party = new();
        bool _atStation;

        public string? ServerPartyId { get; private set; }
        public string? ActiveRunId { get; private set; }
        public string? ActiveMissionId { get; private set; }
        public string? ActiveMissionTitle { get; private set; }
        public string BayId { get; private set; } = "bay-local-1";

        public IReadOnlyList<AgentSlot> Party => _party;
        public int PartyCount => _party.Count;
        public bool HasParty => _party.Count > 0;
        public bool CanEnterStation => HasParty && !_atStation;
        public bool IsAtStation => _atStation;
        public bool IsPartyFull => _party.Count >= MaxAgents;

        /// <summary>파티 대표(첫 슬롯). Nobody면 시스템 기본값 사용.</summary>
        public RuntimeUserData? LeadAgent => _party.Count > 0 ? _party[0].User : null;

        /// <summary>호환용 — LeadAgent와 동일.</summary>
        public RuntimeUserData? User => LeadAgent;

        public bool IsNobody => _party.Count > 0 && _party[0].IsNobody;
        public bool UsesSystemDefaults => IsNobody;
        public bool IsCleared => HasParty;

        /// <summary>온라인 로그인에 성공한 마지막 요원 userId (토큰은 ApiClient).</summary>
        public string? LastAuthenticatedUserId { get; private set; }

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
            PinkSoftApiClient.EnsureOn(gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>없으면 생성. 모든 씬에서 안전하게 호출.</summary>
        public static AgentSession Ensure()
        {
            if (Instance != null)
            {
                PinkSoftApiClient.EnsureOn(Instance.gameObject);
                return Instance;
            }

            var go = new GameObject("AgentSession");
            return go.AddComponent<AgentSession>();
        }

        /// <summary>필수 접근. 없으면 생성 후 반환.</summary>
        public static AgentSession Require() => Ensure();

        public static bool TryGet(out AgentSession session)
        {
            session = Instance!;
            return Instance != null;
        }

        public bool TryGetSlot(int index, out AgentSlot slot)
        {
            if (index < 0 || index >= _party.Count)
            {
                slot = default;
                return false;
            }

            slot = _party[index];
            return true;
        }

        public bool TryGetByCallsign(string callsign, out AgentSlot slot)
        {
            for (var i = 0; i < _party.Count; i++)
            {
                if (string.Equals(_party[i].User.nickname, callsign, System.StringComparison.OrdinalIgnoreCase))
                {
                    slot = _party[i];
                    return true;
                }
            }

            slot = default;
            return false;
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

        public void SetBayId(string? bayId)
        {
            if (!string.IsNullOrWhiteSpace(bayId))
                BayId = bayId.Trim();
        }

        public void NoteAuthenticatedUser(string? userId)
        {
            if (!string.IsNullOrEmpty(userId))
                LastAuthenticatedUserId = userId;
        }

        public void EnterStation()
        {
            if (!HasParty)
                return;
            _atStation = true;
        }

        /// <summary>파티 확정 후 Station 씬으로 이동.</summary>
        public void EnterStationAndLoadScene()
        {
            EnterStation();
            if (_atStation)
                SceneManager.LoadScene(StationSceneName);
        }

        public void LeaveStation() => _atStation = false;

        /// <summary>접선 화면으로. 파티는 유지한 채 Station만 이탈할 때 사용.</summary>
        public void ReturnToRendezvous(bool keepParty = true)
        {
            _atStation = false;
            if (!keepParty)
                Revoke();
            ClearActiveRun();
            SceneManager.LoadScene(RendezvousSceneName);
        }

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

        /// <summary>파티 전체 해제. 씬 전환은 호출측에서.</summary>
        public void Revoke()
        {
            _party.Clear();
            _atStation = false;
            ServerPartyId = null;
            LastAuthenticatedUserId = null;
            ClearActiveRun();
        }

        /// <summary>클리어런스 해제 후 접선 씬.</summary>
        public void RevokeAndReturnToRendezvous()
        {
            Revoke();
            SceneManager.LoadScene(RendezvousSceneName);
        }

        /// <summary>BDS Check 종료 시 복귀할 씬.</summary>
        public string ResolveBdsReturnScene() =>
            _atStation ? StationSceneName : RendezvousSceneName;

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
