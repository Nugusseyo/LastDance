using System;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>행동 하나에 거는 발동 조건. 충족되지 않으면 그 행동을 건너뛰고 다음으로 넘어간다.
    /// CustomerState 안에 [SerializeReference]로 중첩 직렬화되므로, 이름을 옮길 때는 [MovedFrom]을 붙여야 참조가 끊기지 않는다.</summary>
    [Serializable]
    public abstract class StateCondition
    {
        /// <summary>지금 이 손님에게 조건이 맞는지. 매 실행 직전에 평가되므로 런타임에 달라지는 값을 봐도 된다.</summary>
        public abstract bool IsMet(CustomerContext ctx);
    }
}
