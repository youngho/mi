using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

namespace PinkSoft.BDS.Input
{
    public sealed class TouchInputSource : MonoBehaviour, IInputSource
    {
        public string SourceName => "Touch";
        public bool IsAvailable => Touchscreen.current != null || Mouse.current != null || Application.isEditor;

        public event System.Action<InputHit>? OnHit;

        public void Enable() => EnhancedTouchSupport.Enable();

        public void Disable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            if (Mouse.current?.leftButton.wasPressedThisFrame == true)
                FireHit(Mouse.current.position.ReadValue());

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                    FireHit(touch.position.ReadValue());
            }
        }

        void FireHit(Vector2 pos)
        {
            var ts = (ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000);
            OnHit?.Invoke(new InputHit(pos, ts));
        }
    }
}
