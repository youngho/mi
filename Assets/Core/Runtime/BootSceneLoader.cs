using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinkSoft.Core
{
    /// <summary>Boot 씬에서 Core 초기화 후 Lobby 씬으로 전환.</summary>
    public sealed class BootSceneLoader : MonoBehaviour
    {
        [SerializeField] string lobbySceneName = "Lobby";

        void Start()
        {
            if (!string.IsNullOrEmpty(lobbySceneName))
                SceneManager.LoadScene(lobbySceneName);
        }
    }
}
