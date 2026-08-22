namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>
    /// 손님이 가게에 머무는 단계. 스스로는 절대 진행하지 않는다.
    /// 청소가 끝났는지 판단하는 것은 방문의 일이 아니므로,
    /// VisitSession.RequestDeparture()가 불릴 때까지 여기 머문다.
    /// </summary>
    public sealed class WaitingState : IVisitState
    {
        public VisitPhase Phase => VisitPhase.Waiting;

        public void Enter(VisitContext context) { }

        public VisitPhase Tick(VisitContext context, float dt) => VisitPhase.Waiting;
    }
}
