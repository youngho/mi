using System;
using UnityEngine;

namespace PinkSoft.MissionSDK
{
    /// <summary>Core가 미션에 전달하는 가공된 적중 좌표.</summary>
    public readonly struct InputHit
    {
        public readonly Vector2 ScreenPosition;
        public readonly ulong TimestampUs;

        public InputHit(Vector2 screenPosition, ulong timestampUs)
        {
            ScreenPosition = screenPosition;
            TimestampUs = timestampUs;
        }
    }

    /// <summary>하드웨어 입력 구현체 계약 (BDS/Touch/Debug). Core 전용.</summary>
    public interface IInputSource
    {
        string SourceName { get; }
        bool IsAvailable { get; }
        event Action<InputHit> OnHit;
        void Enable();
        void Disable();
    }

    /// <summary>활성 미션에 Core가 라우팅하는 입력 채널. 미션은 이것만 구독.</summary>
    public interface IMissionInput
    {
        event Action<InputHit> OnHit;
    }

    /// <summary>미션 초기화 시 Core가 주입하는 컨텍스트.</summary>
    [Serializable]
    public class MissionContext
    {
        public IMissionInput input = null!;
        public MissionConfig config = new();

        public IMissionInput Input => input;
        public MissionConfig Config => config;
    }

    /// <summary>미션의 OnHit 구독/해제 헬퍼.</summary>
    public sealed class MissionInputSubscription : IDisposable
    {
        IMissionInput? _input;
        Action<InputHit>? _handler;

        public void Subscribe(IMissionInput input, Action<InputHit> handler)
        {
            Unsubscribe();
            _input = input;
            _handler = handler;
            input.OnHit += handler;
        }

        public void Unsubscribe()
        {
            if (_input != null && _handler != null)
                _input.OnHit -= _handler;
            _input = null;
            _handler = null;
        }

        public void Dispose() => Unsubscribe();
    }

    public static class MissionHitUtility
    {
        public static bool TryRaycast(InputHit hit, LayerMask layer, out RaycastHit result, float maxDistance = 100f)
        {
            result = default;
            var cam = Camera.main;
            if (cam == null)
                return false;
            var ray = cam.ScreenPointToRay(hit.ScreenPosition);
            return Physics.Raycast(ray, out result, maxDistance, layer);
        }
    }
}
