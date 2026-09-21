using System;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>차량이 퇴장 지점까지 빠져나간다.</summary>
    [Serializable]
    public sealed class LeavingState : VisitState
    {
        [Tooltip("퇴장에 쓸 수 있는 한계 시간(초). 없으면 막힌 방문이 Completed에 도달하지 못해 자원이 회수되지 않는다.")]
        [SerializeField, Min(0f)] private float phaseTimeout = 30f;

        public override VisitPhase Phase => VisitPhase.Leaving;

        public override void Enter(VisitContext context)
        {
            context.Car.MoveTo(context.ExitPoint);
        }

        public override VisitPhase Tick(VisitContext context, float dt)
        {
            if (context.Car.IsArrived)
            {
                return VisitPhase.Completed;
            }

            context.PhaseElapsed += dt;

            if (context.PhaseElapsed < phaseTimeout && context.Car.HasCompletePath)
            {
                return VisitPhase.Leaving;
            }

            // 퇴장 실패는 회복할 방법이 없다. 방문을 닫아 차·손님·자리를 회수하는 편이 낫다.
            Debug.LogWarning($"[Leaving] {context.Car.name}이(가) 퇴장하지 못해 방문을 강제로 종료합니다. 퇴장 지점이 NavMesh 위에 있는지 확인하세요.", context.Car);

            return VisitPhase.Completed;
        }
    }
}
