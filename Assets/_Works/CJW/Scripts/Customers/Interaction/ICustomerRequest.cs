using System;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Interaction
{
    /// <summary>손님이 플레이어에게 무언가를 요구하는 창구. 요구를 거는 쪽(손님 상태)과 답하는 쪽(플레이어 상호작용)이
    /// 서로의 클래스를 모르고도 만나도록 이 계약 하나만 공유한다.
    /// 한 손님은 한 번에 하나의 요구만 건다 — 두 개를 겹쳐 걸면 답한 쪽이 무엇에 답했는지 알 수 없다.</summary>
    public interface ICustomerRequest
    {
        /// <summary>요구한 손님의 오브젝트. 답하는 쪽이 누구에게 답하는지 알아야 해서 노출한다.</summary>
        GameObject Owner { get; }

        /// <summary>답을 기다리는 중인지.</summary>
        bool IsPending { get; }

        /// <summary>지금 걸려 있는(또는 마지막에 걸렸던) 요구의 종류.</summary>
        CustomerRequestType Type { get; }

        /// <summary>요구에 딸린 수치. 네고라면 깎아 달라는 금액처럼 종류마다 뜻이 다르다.</summary>
        float Amount { get; }

        /// <summary>마지막 결과. 답을 기다리는 동안에는 <see cref="CustomerRequestResult.Pending"/>이다.</summary>
        CustomerRequestResult Result { get; }

        /// <summary>요구가 걸릴 때 발생. UI나 아이콘이 이걸 보고 뜬다.</summary>
        event Action<ICustomerRequest> Raised;

        /// <summary>요구가 끝날 때 발생. 어떻게 끝났든 반드시 한 번 불린다.</summary>
        event Action<ICustomerRequest, CustomerRequestResult> Resolved;

        /// <summary>요구를 건다. 이미 걸려 있으면 앞의 요구를 <see cref="CustomerRequestResult.Cancelled"/>로 닫고 새로 건다.</summary>
        void Raise(CustomerRequestType type, float amount);

        /// <summary>들어준다.</summary>
        void Accept();

        /// <summary>거절한다.</summary>
        void Reject();

        /// <summary>요구를 거둔다. 기다리다 지쳤을 때는 <paramref name="expired"/>를 켠다.</summary>
        void Withdraw(bool expired = false);
    }
}
