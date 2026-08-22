namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>
    /// 방문의 한 단계. 다음 단계는 Tick의 반환값으로만 결정된다.
    /// 자기 Phase를 그대로 돌려주면 현재 단계를 유지한다.
    /// </summary>
    public interface IVisitState
    {
        VisitPhase Phase { get; }

        void Enter(VisitContext context);

        VisitPhase Tick(VisitContext context, float dt);
    }
}
