namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>손님들이 각자 차로 돌아와 탑승을 끝낼 때까지 기다린다. 좌석은 이미 배정되어 있어 세션이 순서를 통제할 필요가 없다.</summary>
    public sealed class BoardingState : IVisitState
    {
        public VisitPhase Phase => VisitPhase.Boarding;

        public void Enter(VisitContext context)
        {
        }

        public VisitPhase Tick(VisitContext context, float dt)
        {
            return context.CustomerPhaseDone ? VisitPhase.Leaving : VisitPhase.Boarding;
        }
    }
}
