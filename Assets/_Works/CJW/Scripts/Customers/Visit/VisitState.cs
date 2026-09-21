using System;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>방문의 한 단계. 다음 단계는 Tick의 반환값으로만 결정되며, 자기 Phase를 그대로 돌려주면 현재 단계를 유지한다.
    /// CarDataSO에 [SerializeReference]로 꽂혀 차종마다 다른 연출을 쓸 수 있으므로,
    /// <b>진행 상태(타이머·커서)를 필드로 들지 않는다.</b> 그런 값은 VisitContext가 소유한다 —
    /// 하나의 인스턴스를 동시 방문 여러 개가 나눠 쓰기 때문이다.</summary>
    [Serializable]
    public abstract class VisitState
    {
        public abstract VisitPhase Phase { get; }

        public abstract void Enter(VisitContext context);

        public abstract VisitPhase Tick(VisitContext context, float dt);
    }
}
