using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core
{
    /// <summary>
    /// 에이전트(플레이어) 신원 확인 세션. Rendezvous(Station) 진입 전 필수.
    /// </summary>
    public sealed class AgentSession : MonoBehaviour
    {
        public static AgentSession? Instance { get; private set; }

        RuntimeUserData? _user;
        bool _cleared;

        public bool IsCleared => _cleared && _user != null;
        public RuntimeUserData? User => _user;

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

        public void ClearIdentity(RuntimeUserData user)
        {
            _user = user;
            _cleared = true;
        }

        public void Revoke()
        {
            _user = null;
            _cleared = false;
        }
    }
}
