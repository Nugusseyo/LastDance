using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>
    /// 자기 좌석으로 걸어가 탑승한다.
    ///
    /// 좌석 번호만큼 간격을 두고 출발하므로 한 차의 손님들이 겹쳐 움직이지 않는다.
    /// 좌석은 <see cref="CustomerContext.SeatIndex"/>로 이미 정해져 있어
    /// 도착 순서가 뒤섞여도 좌석 배정이 어긋나지 않는다.
    /// </summary>
    [Serializable]
    public sealed class BoardState : CustomerState
    {
        [Tooltip("좌석 번호 × 이 간격만큼 기다린 뒤 출발한다. 0이면 차의 BoardingInterval을 쓴다.")]
        [SerializeField, Min(0f)] private float stagger;

        [Tooltip("출발 간격에 더해지는 무작위 흔들림. 0이면 정확히 같은 간격으로 움직인다.")]
        [SerializeField, Min(0f)] private float jitter = 0.2f;

        [Tooltip("이 시간 안에 차에 닿지 못하면 그 자리에서 탑승 처리한다. 0이면 무제한.")]
        [SerializeField, Min(0f)] private float timeout = 20f;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            VisitContext visit = Ctx.Visit;
            Car car = visit?.Car;
            if (car == null)
            {
                return VisitOutcome.Failed;
            }

            float interval = stagger > 0f ? stagger : visit.Interval;
            float delay = interval * Ctx.SeatIndex;

            if (jitter > 0f)
            {
                delay += Random.Range(0f, jitter);
            }

            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }

            // 좌석이 모자라면 차 원점에 붙인다. 어긋난 사실은 VisitSession.Begin에서 이미 에러로 남겼다.
            Transform seat = car.HasSeat(Ctx.SeatIndex) ? car.GetSeat(Ctx.SeatIndex) : car.transform;

            VisitOutcome moved = await MoveAndWait(seat.position, timeout, ct);

            AbstractCustomer customer = Ctx.Customer;
            if (customer.Boarding == null)
            {
                // 여기서 멈추면 방문 전체가 굳는다. 태우지 못한 사실만 남기고 넘어간다.
                Debug.LogError(
                    $"[BoardState] {customer.name}에 탑승 모듈이 없어 태우지 못했습니다. " +
                    "프리팹에 BoardingModule을 붙여야 합니다.", customer);
                return VisitOutcome.Blocked;
            }

            // 못 걸어왔더라도 태운다. 안 그러면 손님 하나 때문에 차가 영영 출발하지 못한다.
            customer.Boarding.Board(seat);
            Debug.Log("<size=12><color=blue> 탑승 </color></size>");
            
            return moved;
        }
    }
}
