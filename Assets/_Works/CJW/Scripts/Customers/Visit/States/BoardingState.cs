namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>
    /// 손님들이 각자 차로 돌아와 탑승을 끝낼 때까지 기다린다.
    ///
    /// 좌석은 CustomerContext.SeatIndex로 방문 시작 때 이미 정해져 있으므로
    /// 도착 순서가 뒤섞여도 좌석 배정이 어긋나지 않는다. 세션이 순서를 통제할 필요가 없다.
    /// </summary>
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
