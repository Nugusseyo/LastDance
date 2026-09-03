namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>
    /// 손님들이 각자 하차를 끝낼 때까지 기다린다.
    ///
    /// 예전에는 세션이 커서를 돌리며 한 명씩 내리고 목적지까지 지정했지만,
    /// 이제 "어떻게 내리는가"는 손님의 시퀀스가 안다. 세션은 전원 끝났는지만 본다.
    /// 안 내리는 손님은 Unloading 칸이 비어 있어 즉시 끝난다.
    /// </summary>
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
