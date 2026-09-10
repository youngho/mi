using UnityEngine;
using UnityEngine.SceneManagement;

namespace PinkSoft.Core
{
    /// <summary>Boot 씬에서 Core 초기화 후 하드웨어 검증 씬(BdsCheck)으로 전환.</summary>
    public sealed class BootSceneLoader : MonoBehaviour
    {
        [SerializeField] string nextSceneName = "BdsCheck";

        void Start()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
        }
    }
}
