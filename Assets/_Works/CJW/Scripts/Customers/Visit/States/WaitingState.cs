using System;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>손님이 가게에 머무는 단계. 스스로는 진행하지 않고 VisitSession의 RequestDeparture()나 Repel()이 불릴 때까지 머문다.</summary>
    [Serializable]
    public sealed class WaitingState : VisitState
    {
        [Tooltip("여기 머물 수 있는 한계 시간(초). 0이면 무한히 기다린다. " +
                 "이 단계는 밖에서 불러 주지 않으면 스스로 빠져나가지 못하는데, 굳어 버리면 주차 자리가 영영 반납되지 않아 " +
                 "스폰 전체가 멈춘다. 그래서 다른 단계보다 넉넉하게 두되 안전망은 남긴다.")]
        [SerializeField, Min(0f)] private float phaseTimeout = 120f;

        public override VisitPhase Phase => VisitPhase.Waiting;

        public override void Enter(VisitContext context) { }

        public override VisitPhase Tick(VisitContext context, float dt)
        {
            if (phaseTimeout <= 0f)
            {
                return VisitPhase.Waiting;
            }

            context.PhaseElapsed += dt;

            if (context.PhaseElapsed < phaseTimeout)
            {
                return VisitPhase.Waiting;
            }

            Debug.LogWarning(
                $"[Waiting] {context.Car.name}이(가) {phaseTimeout}초 동안 아무 요청도 받지 못해 스스로 출발합니다. " +
                "퇴치나 출발 요청이 실제로 연결되어 있는지 확인하세요.", context.Car);

            return VisitPhase.Boarding;
        }
    }
}
