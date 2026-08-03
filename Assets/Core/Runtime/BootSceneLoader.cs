using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinkSoft.Core
{
    /// <summary>Boot 씬에서 Core 초기화 후 Rendezvous(접선) 씬으로 전환.</summary>
    public sealed class BootSceneLoader : MonoBehaviour
    {
        [SerializeField] string rendezvousSceneName = "Rendezvous";

        void Start()
        {
            if (!string.IsNullOrEmpty(rendezvousSceneName))
                SceneManager.LoadScene(rendezvousSceneName);
        }
    }
}
