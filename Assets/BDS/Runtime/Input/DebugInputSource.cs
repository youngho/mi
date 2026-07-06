using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PinkSoft.BDS.Input
{
    public sealed class DebugInputSource : MonoBehaviour, IInputSource
    {
        [SerializeField] Key fireKey = Key.Space;
        [SerializeField] Vector2 normalizedPosition = new(0.5f, 0.5f);

        public string SourceName => "Debug";
        public bool IsAvailable => Keyboard.current != null;

        public event System.Action<InputHit>? OnHit;

        public void Enable() { }
        public void Disable() { }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[fireKey].wasPressedThisFrame)
                return;

            var pos = new Vector2(
                normalizedPosition.x * Screen.width,
                normalizedPosition.y * Screen.height);
            var ts = (ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000);
            OnHit?.Invoke(new InputHit(pos, ts));
        }
    }
}
