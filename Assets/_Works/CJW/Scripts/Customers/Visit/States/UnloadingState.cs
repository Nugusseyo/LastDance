namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>손님들이 각자 하차를 끝낼 때까지 기다린다. "어떻게 내리는가"는 손님의 시퀀스가 알고, 세션은 전원 끝났는지만 본다.</summary>
    public sealed class UnloadingState : IVisitState
    {
        public VisitPhase Phase => VisitPhase.Unloading;

        public void Enter(VisitContext context)
        {
        }

        public VisitPhase Tick(VisitContext context, float dt)
        {
            return context.CustomerPhaseDone ? VisitPhase.Waiting : VisitPhase.Unloading;
        }
    }
}
