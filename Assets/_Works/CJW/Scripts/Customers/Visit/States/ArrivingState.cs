namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>차량이 정차 지점까지 들어온다.</summary>
    public sealed class ArrivingState : IVisitState
    {
        public VisitPhase Phase => VisitPhase.Arriving;

        public void Enter(VisitContext context)
        {
            context.Car.MoveTo(context.ArrivalPoint);
        }

        public VisitPhase Tick(VisitContext context, float dt)
        {
            if (!context.Car.IsArrived)
            {
                return VisitPhase.Arriving;
            }

            context.Car.Stop();
            return VisitPhase.Unloading;
        }
    }
}
