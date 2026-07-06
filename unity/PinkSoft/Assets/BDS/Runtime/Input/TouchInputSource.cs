using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.BDS.Input
{
    public sealed class TouchInputSource : MonoBehaviour, IInputSource
    {
        public string SourceName => "Touch";
        public bool IsAvailable => Input.touchSupported || Application.isEditor;

        public event System.Action<InputHit>? OnHit;

        public void Enable() { }
        public void Disable() { }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
                FireHit(Input.mousePosition);

            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                    FireHit(t.position);
            }
        }

        void FireHit(Vector2 pos)
        {
            var ts = (ulong)(Time.realtimeSinceStartupAsDouble * 1_000_000);
            OnHit?.Invoke(new InputHit(pos, ts));
        }
    }
}
