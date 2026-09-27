using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Patience
{
    /// <summary>인내심의 기본 구현. 기다리기 시작한 시각과 참을 시간만 들고, 남은 양은 물을 때마다 계산한다 — 매 프레임 돌 일이 없다.</summary>
    [DisallowMultipleComponent]
    public sealed class CustomerPatienceModule : AbstractModule, ICustomerPatience
    {
        [Tooltip("기다리는 상태가 참을 시간을 정하지 않았을 때(늦어도 화내지 않는 취한 손님처럼 끝없이 기다리는 경우) 인내심이 바닥나기까지 걸리는 시간(초). " +
                 "보여주기만 할 뿐, 바닥나도 손님은 계속 기다린다.")]
        [SerializeField, Min(1f)] private float fallbackSeconds = 30f;

        public bool IsWaiting { get; private set; }

        public float Normalized
        {
            get
            {
                if (!IsWaiting)
                {
                    return 1f;
                }

                return 1f - Mathf.Clamp01((Time.time - _startTime) / _limit);
            }
        }

        private float _startTime;
        private float _limit;

        public void Begin(float seconds)
        {
            IsWaiting = true;
            _startTime = Time.time;
            _limit = seconds > 0f ? seconds : fallbackSeconds;
        }

        public void End()
        {
            IsWaiting = false;
            _limit = 0f;
        }
    }
}
