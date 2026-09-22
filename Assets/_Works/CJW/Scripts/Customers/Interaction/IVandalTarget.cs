using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Interaction
{
    /// <summary>손님에게 맞을 수 있는 물건. 자판기든 차든 때리는 쪽은 이 계약만 알면 되므로,
    /// 상태가 자판기 클래스나 차 클래스를 직접 참조하지 않는다.
    /// 구현체가 없어도 손님은 때리는 연출을 그대로 한다 — 맞는 쪽의 반응만 없을 뿐이다.</summary>
    public interface IVandalTarget
    {
        /// <summary>지금 맞을 수 있는지. 이미 부서졌거나 수리 중이면 false를 돌려 손님이 헛되이 때리지 않게 한다.</summary>
        bool CanTakeHit { get; }

        /// <summary>손님이 이 물건을 때렸다. 무엇이 일어나는지는 구현체가 정한다.</summary>
        void TakeVandalHit(in VandalHit hit);
    }
}
