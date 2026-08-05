using System;
using System.Collections;
using System.Text;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.Networking;

namespace PinkSoft.Core
{
    /// <summary>pinkapi MI 모듈 클라이언트 (/mi/api/…).</summary>
    public sealed class PinkSoftApiClient : MonoBehaviour
    {
        [SerializeField] string baseUrl = "http://localhost:8080";
        [SerializeField] string apiRoot = "/mi/api";

        string? _token;
        string? _userId;
        string? _nickname;
        string? _lastRunId;

        public string? Token => _token;
        public string? UserId => _userId;
        public string? Nickname => _nickname;
        public string? LastRunId => _lastRunId;

        string Api(string path)
        {
            var root = (baseUrl ?? "").TrimEnd('/');
            var prefix = (apiRoot ?? "/mi/api").TrimEnd('/');
            if (!prefix.StartsWith('/'))
                prefix = "/" + prefix;
            var p = path.StartsWith('/') ? path : "/" + path;
            return root + prefix + p;
        }

        public IEnumerator Login(string nickname, Action<bool> onComplete)
        {
            var body = JsonUtility.ToJson(new LoginRequest
            {
                nickname = nickname,
                deviceId = SystemInfo.deviceUniqueIdentifier
            });
            using var req = PostJson(Api("/auth/login"), body, withAuth: false);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MI] login failed: {req.responseCode} {req.error} {req.downloadHandler?.text}");
                onComplete(false);
                yield break;
            }

            var res = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
            _token = res.token;
            _userId = res.userId;
            _nickname = string.IsNullOrEmpty(res.nickname) ? nickname : res.nickname;
            onComplete(true);
        }

        public IEnumerator FetchCatalog(string? category, Action<CatalogResponse?> onComplete)
        {
            var path = string.IsNullOrEmpty(category)
                ? "/missions/catalog"
                : $"/missions/catalog?category={UnityWebRequest.EscapeURL(category)}";
            using var req = Get(Api(path), withAuth: false);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MI] catalog failed: {req.responseCode} {req.error}");
                onComplete(null);
                yield break;
            }

            onComplete(JsonUtility.FromJson<CatalogResponse>(req.downloadHandler.text));
        }

        public IEnumerator CreateParty(string? bayId, PartyMemberRequest[] members, Action<PartyResponse?> onComplete)
        {
            var body = JsonUtility.ToJson(new CreatePartyRequest
            {
                bayId = bayId ?? "",
                members = members ?? Array.Empty<PartyMemberRequest>()
            });
            using var req = PostJson(Api("/parties"), body, withAuth: true);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MI] create party failed: {req.responseCode} {req.error} {req.downloadHandler?.text}");
                onComplete(null);
                yield break;
            }

            onComplete(JsonUtility.FromJson<PartyResponse>(req.downloadHandler.text));
        }

        public IEnumerator StartRun(string missionId, string? partyId, string? bayId, Action<RunResponse?> onComplete)
        {
            var body = JsonUtility.ToJson(new CreateRunRequest
            {
                missionId = missionId,
                partyId = partyId ?? "",
                bayId = bayId ?? "",
                members = string.IsNullOrEmpty(_userId) ? Array.Empty<string>() : new[] { _userId }
            });
            using var req = PostJson(Api("/runs"), body, withAuth: true);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MI] start run failed: {req.responseCode} {req.error} {req.downloadHandler?.text}");
                onComplete(null);
                yield break;
            }

            var res = JsonUtility.FromJson<RunResponse>(req.downloadHandler.text);
            _lastRunId = res.runId;
            onComplete(res);
        }

        public IEnumerator CompleteMission(MissionResultData result, string missionId, Action<CompleteResponse?> onComplete)
            => CompleteMission(result, missionId, _lastRunId, onComplete);

        public IEnumerator CompleteMission(
            MissionResultData result,
            string missionId,
            string? runId,
            Action<CompleteResponse?> onComplete)
        {
            var payload = new CompleteRequest
            {
                runId = runId ?? "",
                missionId = missionId,
                finalScore = result.finalScore,
                playTime = result.playTime,
                starsEarned = result.starsEarned,
                eventLog = result.eventLog?.ToArray() ?? Array.Empty<ScoreEventRecord>()
            };
            var body = JsonUtility.ToJson(payload);
            using var req = PostJson(Api("/mission/complete"), body, withAuth: true);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MI] complete failed: {req.responseCode} {req.error} {req.downloadHandler?.text}");
                onComplete(null);
                yield break;
            }

            onComplete(JsonUtility.FromJson<CompleteResponse>(req.downloadHandler.text));
        }

        public IEnumerator FetchRanking(string missionId, int limit, Action<RankingResponse?> onComplete)
        {
            using var req = Get(Api($"/ranking/{UnityWebRequest.EscapeURL(missionId)}?limit={limit}"), withAuth: false);
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MI] ranking failed: {req.responseCode} {req.error}");
                onComplete(null);
                yield break;
            }

            onComplete(JsonUtility.FromJson<RankingResponse>(req.downloadHandler.text));
        }

        UnityWebRequest Get(string url, bool withAuth)
        {
            var req = UnityWebRequest.Get(url);
            if (withAuth && !string.IsNullOrEmpty(_token))
                req.SetRequestHeader("Authorization", $"Bearer {_token}");
            return req;
        }

        UnityWebRequest PostJson(string url, string body, bool withAuth)
        {
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            if (withAuth && !string.IsNullOrEmpty(_token))
                req.SetRequestHeader("Authorization", $"Bearer {_token}");
            return req;
        }

        [Serializable]
        class LoginRequest
        {
            public string nickname = "";
            public string deviceId = "";
        }

        [Serializable]
        class LoginResponse
        {
            public string token = "";
            public string userId = "";
            public string nickname = "";
        }

        [Serializable]
        public class CatalogResponse
        {
            public MissionMeta[] missions = Array.Empty<MissionMeta>();
        }

        [Serializable]
        public class MissionMeta
        {
            public string missionId = "";
            public string title = "";
            public string description = "";
            public string author = "";
            public string version = "";
            public string bundleUrl = "";
            public int requiredLevel;
            public int entryFee;
            public int timeLimit;
            public int targetScore;
            public string category = "";
        }

        [Serializable]
        public class PartyMemberRequest
        {
            public string userId = "";
            public string nickname = "";
            public bool isNobody;
        }

        [Serializable]
        class CreatePartyRequest
        {
            public string bayId = "";
            public PartyMemberRequest[] members = Array.Empty<PartyMemberRequest>();
        }

        [Serializable]
        public class PartyResponse
        {
            public string partyId = "";
            public string bayId = "";
            public PartyMemberRequest[] members = Array.Empty<PartyMemberRequest>();
        }

        [Serializable]
        class CreateRunRequest
        {
            public string missionId = "";
            public string partyId = "";
            public string bayId = "";
            public string[] members = Array.Empty<string>();
        }

        [Serializable]
        public class RunResponse
        {
            public string runId = "";
            public string missionId = "";
            public string partyId = "";
            public string bayId = "";
            public string startedAt = "";
        }

        [Serializable]
        class CompleteRequest
        {
            public string runId = "";
            public string missionId = "";
            public int finalScore;
            public int playTime;
            public int starsEarned;
            public ScoreEventRecord[] eventLog = Array.Empty<ScoreEventRecord>();
        }

        [Serializable]
        public class CompleteResponse
        {
            public int goldReward;
            public int expGained;
            public int newRank;
            public bool validated;
        }

        [Serializable]
        public class RankingResponse
        {
            public RankingEntry[] entries = Array.Empty<RankingEntry>();
        }

        [Serializable]
        public class RankingEntry
        {
            public int rank;
            public string userId = "";
            public string nickname = "";
            public int score;
            public int playTime;
        }
    }
}
