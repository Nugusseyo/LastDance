using System;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Interaction
{
    /// <summary>요구 창구의 기본 구현. 상태를 들고 이벤트를 쏘는 일만 하고, 요구를 들어줬을 때 실제로 무엇이 일어나는지는
    /// 답한 쪽이 정한다 — 손님은 답만 듣는다.</summary>
    [DisallowMultipleComponent]
    public sealed class CustomerRequestModule : AbstractModule, ICustomerRequest
    {
        public GameObject Owner => _owner != null ? _owner.gameObject : gameObject;

        public bool IsPending => Result == CustomerRequestResult.Pending && Type != CustomerRequestType.None;

        public CustomerRequestType Type { get; private set; }

        public float Amount { get; private set; }

        public CustomerRequestResult Result { get; private set; } = CustomerRequestResult.Cancelled;

        public event Action<ICustomerRequest> Raised;
        public event Action<ICustomerRequest, CustomerRequestResult> Resolved;

        public void Raise(CustomerRequestType type, float amount)
        {
            if (type == CustomerRequestType.None)
            {
                Debug.LogError($"[{nameof(CustomerRequestModule)}] {name}이(가) None 요구를 걸려 했습니다. 종류를 정해야 합니다.", this);
                return;
            }

            // 앞의 요구를 열어 둔 채 새로 걸면, 답한 쪽은 자기가 무엇에 답했는지 알 수 없다. 먼저 닫는다.
            if (IsPending)
            {
                Resolve(CustomerRequestResult.Cancelled);
            }

            Type = type;
            Amount = amount;
            Result = CustomerRequestResult.Pending;

            Raised?.Invoke(this);
        }

        public void Accept() => Resolve(CustomerRequestResult.Accepted);

        public void Reject() => Resolve(CustomerRequestResult.Rejected);

        public void Withdraw(bool expired = false)
            => Resolve(expired ? CustomerRequestResult.Expired : CustomerRequestResult.Cancelled);

        private void Resolve(CustomerRequestResult result)
        {
            // 답을 두 번 받으면 기다리던 쪽이 뒤엣것을 보고 엉뚱하게 굴거나, 보상이 두 번 나간다.
            if (Result != CustomerRequestResult.Pending)
            {
                return;
            }

            Result = result;
            Resolved?.Invoke(this, result);
        }
    }
}
