using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.BDS.Input
{
    public sealed class DebugInputSource : MonoBehaviour, IInputSource
    {
        [SerializeField] KeyCode fireKey = KeyCode.Space;
        [SerializeField] Vector2 normalizedPosition = new(0.5f, 0.5f);

        public string SourceName => "Debug";
        public bool IsAvailable => true;

        public event System.Action<InputHit>? OnHit;

        public void Enable() { }
        public void Disable() { }

        void Update()
        {
            if (Input.GetKeyDown(fireKey))
            {
                var pos = new Vector2(
                    normalizedPosition.x * Screen.width,
                    normalizedPosition.y * Screen.height);
                var ts = (ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000);
                OnHit?.Invoke(new InputHit(pos, ts));
            }
        }
    }
}
