using System;
using PinkSoft.MissionSDK;
using UnityEngine;

namespace PinkSoft.Core
{
    /// <summary>활성 미션 1개에만 InputHit를 라우팅. BDS/Touch 소스와 미션 사이 중간 계층.</summary>
    public sealed class MissionInputRouter : MonoBehaviour, IMissionInput
    {
        IInputSource? _source;
        bool _paused = true;

        public event Action<InputHit>? OnHit;

        public bool IsRouting => !_paused && _source != null;

        public void Configure(IInputSource source)
        {
            if (_source != null)
                _source.OnHit -= ForwardHit;

            _source = source;
            if (_source != null)
                _source.OnHit += ForwardHit;
        }

        public void Bind()
        {
            _paused = false;
            _source?.Enable();
        }

        public void Unbind()
        {
            _paused = true;
        }

        public void SetPaused(bool paused) => _paused = paused;

        void ForwardHit(InputHit hit)
        {
            if (!_paused)
                OnHit?.Invoke(hit);
        }

        void OnDestroy()
        {
            if (_source != null)
                _source.OnHit -= ForwardHit;
        }
    }
}
