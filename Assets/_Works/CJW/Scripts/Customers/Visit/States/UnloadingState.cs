namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>손님을 한 명씩 간격을 두고 내린다.</summary>
    public sealed class UnloadingState : IVisitState
    {
        private float _timer;
        private int _cursor;

        public VisitPhase Phase => VisitPhase.Unloading;

        public void Enter(VisitContext context)
        {
            _timer = 0f;
            _cursor = 0;
        }

        public VisitPhase Tick(VisitContext context, float dt)
        {
            if (_cursor >= context.Customers.Count)
            {
                return VisitPhase.Waiting;
            }

            _timer -= dt;
            if (_timer > 0f)
            {
                return VisitPhase.Unloading;
            }

            context.Customers[_cursor].Unboard(context.Car.DropOffPosition, context.ShopPoint);
            _cursor++;
            _timer = context.Interval;

            return _cursor >= context.Customers.Count ? VisitPhase.Waiting : VisitPhase.Unloading;
        }
    }
}
