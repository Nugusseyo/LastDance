namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>차량이 퇴장 지점까지 빠져나간다.</summary>
    public sealed class LeavingState : IVisitState
    {
        public VisitPhase Phase => VisitPhase.Leaving;

        public void Enter(VisitContext context)
        {
            context.Car.MoveTo(context.ExitPoint);
        }

        public VisitPhase Tick(VisitContext context, float dt)
        {
            return context.Car.IsArrived ? VisitPhase.Completed : VisitPhase.Leaving;
        }
    }
}
