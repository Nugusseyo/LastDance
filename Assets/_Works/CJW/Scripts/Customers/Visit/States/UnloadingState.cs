using System;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>손님들이 각자 하차를 끝낼 때까지 기다린다. "어떻게 내리는가"는 손님의 시퀀스가 알고, 세션은 전원 끝났는지만 본다.
    /// 손님의 Unloading 시퀀스가 비어 있으면 아무도 내리지 않고 그대로 Waiting으로 넘어간다 — 차에서 안 내리는 손님이 이렇게 만들어진다.</summary>
    [Serializable]
    public sealed class UnloadingState : VisitState
    {
        public override VisitPhase Phase => VisitPhase.Unloading;

        public override void Enter(VisitContext context)
        {
        }

        public override VisitPhase Tick(VisitContext context, float dt)
        {
            return context.CustomerPhaseDone ? VisitPhase.Waiting : VisitPhase.Unloading;
        }
    }
}
