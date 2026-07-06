using System;
using System.Collections;
using System.Text;
using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.Networking;

namespace PinkSoft.Core
{
    /// <summary>백엔드 API 클라이언트 (로그인, 완료, 랭킹).</summary>
    public sealed class PinkSoftApiClient : MonoBehaviour
    {
        [SerializeField] string baseUrl = "http://localhost:3000";

        string? _token;
        string? _userId;

        public string? Token => _token;
        public string? UserId => _userId;

        public IEnumerator Login(string nickname, Action<bool> onComplete)
        {
            var body = JsonUtility.ToJson(new LoginRequest { nickname = nickname });
            using var req = new UnityWebRequest($"{baseUrl}/auth/login", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(req.error);
                onComplete(false);
                yield break;
            }

            var res = JsonUtility.FromJson<LoginResponse>(req.downloadHandler.text);
            _token = res.token;
            _userId = res.userId;
            onComplete(true);
        }

        public IEnumerator CompleteMission(MissionResultData result, string missionId, Action<CompleteResponse?> onComplete)
        {
            var payload = new CompleteRequest
            {
                missionId = missionId,
                finalScore = result.finalScore,
                playTime = result.playTime,
                starsEarned = result.starsEarned,
                eventLog = result.eventLog?.ToArray() ?? Array.Empty<ScoreEventRecord>()
            };
            var body = JsonUtility.ToJson(payload);
            using var req = new UnityWebRequest($"{baseUrl}/mission/complete", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {_token}");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(req.error);
                onComplete(null);
                yield break;
            }

            onComplete(JsonUtility.FromJson<CompleteResponse>(req.downloadHandler.text));
        }

        [Serializable]
        class LoginRequest { public string nickname = ""; }

        [Serializable]
        class LoginResponse { public string token = ""; public string userId = ""; public string nickname = ""; }

        [Serializable]
        class CompleteRequest
        {
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
    }
}
