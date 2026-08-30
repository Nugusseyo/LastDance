using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>
    /// 차에서 내린다. 좌석 번호만큼 간격을 두고 내리므로
    /// 세션이 커서를 돌리며 한 명씩 처리할 필요가 없다.
    /// 내리는 일과 어디로 갈지는 별개다 — 목적지는 다음 상태가 정한다.
    /// </summary>
    [Serializable]
    public sealed class UnboardState : CustomerState
    {
        [Tooltip("좌석 번호 × 이 간격만큼 기다린 뒤 내린다. 0이면 차의 BoardingInterval을 쓴다.")]
        [SerializeField, Min(0f)] private float stagger;
        [Tooltip("하차 간격에 더해지는 무작위 흔들림. 0이면 정확히 같은 간격으로 내린다.")]
        [SerializeField, Min(0f)] private float jitter = 0.2f;


        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            VisitContext visit = Ctx.Visit;
            if (visit?.Car == null)
            {
                return VisitOutcome.Failed;
            }

            float interval = stagger > 0f ? stagger : visit.Interval;
            float delay = interval * Ctx.SeatIndex;

            if (jitter > 0f)
            {
                delay += UnityEngine.Random.Range(0f, jitter);
            }

            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }

            AbstractCustomer customer = Ctx.Customer;
            if (customer.Boarding == null)
            {
                // 여기서 멈추면 방문 전체가 굳는다. 사실만 남기고 넘어간다.
                Debug.LogError(
                    $"[UnboardState] {customer.name}에 탑승 모듈이 없어 내리지 못했습니다. " +
                    "프리팹에 BoardingModule을 붙여야 합니다.", customer);
                return VisitOutcome.Blocked;
            }

            customer.Boarding.Unboard(visit.Car.DropOffPosition);
            return VisitOutcome.Done;
        }
    }
}
