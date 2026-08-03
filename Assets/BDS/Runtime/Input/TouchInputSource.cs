using PinkSoft.MissionSDK;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

namespace PinkSoft.BDS.Input
{
    /// <summary>
    /// Teensy R(laserModuleR) USB HID 마우스 및 로컬 마우스/터치를 InputHit로 변환.
    /// BDS Check의 정식 입력 경로 (Game 뷰 1920×1080 기준).
    /// </summary>
    public sealed class TouchInputSource : MonoBehaviour, IInputSource
    {
        public string SourceName => "HID/Touch";
        public bool IsAvailable => Touchscreen.current != null || Mouse.current != null || Application.isEditor;

        public event System.Action<InputHit>? OnHit;

        public void Enable() => EnhancedTouchSupport.Enable();

        public void Disable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            // Teensy: Mouse.moveTo + Mouse.click → 여기 leftButton
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
