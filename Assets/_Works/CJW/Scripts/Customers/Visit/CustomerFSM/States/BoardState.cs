using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using DevLib.SoundSystem;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>자기 좌석으로 걸어가 탑승한다. 좌석 번호만큼 간격을 두고 출발해 한 차의 손님들이 겹쳐 움직이지 않는다.</summary>
    [Serializable]
    public sealed class BoardState : CustomerState
    {
        [Tooltip("좌석 번호 × 이 간격만큼 기다린 뒤 출발한다. 0이면 차의 BoardingInterval을 쓴다.")]
        [SerializeField, Min(0f)] private float stagger;

        [Tooltip("출발 간격에 더해지는 무작위 흔들림. 0이면 정확히 같은 간격으로 움직인다.")]
        [SerializeField, Min(0f)] private float jitter = 0.2f;

        [Tooltip("이 시간 안에 차에 닿지 못하면 그 자리에서 탑승 처리한다. 0이면 무제한.")]
        [SerializeField, Min(0f)] private float timeout = 40f;

        [Header("사운드")]
        [Tooltip("차에 올라탈 때 낼 소리(차 문 여닫는 소리). 좌석 자리에서 난다.")]
        [SerializeField] private SoundClipSo boardSound;

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

            VisitOutcome moved = await WalkToCar(car, ChooseBoardingPoint(car, seat.position), ct);

            // 걸어오는 사이 차가 사라졌으면(플레이 종료, 차 정리) 탈 자리가 없다.
            if (car == null || seat == null)
            {
                return VisitOutcome.Failed;
            }

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
            customer.Sound?.Play(boardSound, seat.position);
            Debug.Log("<size=12><color=blue> 탑승 </color></size>");
            
            return moved;
        }

        /// <summary>차 몸체에서 이 거리(m) 안에 들어오면 목적지에 못 닿았어도 탄다. 탑승 지점 후보가 차에서 0.7m 떨어져 있고,
        /// 루트 모션으로 걷는 몸은 목적지 1m 앞에서 속도가 줄어 거의 서 버리므로 그만큼을 봐준다.
        /// 직렬화하지 않는다 — [SerializeReference] 상태에 새 필드를 넣으면 기존 프리팹에 0으로 들어간다.</summary>
        private const float DoorReach = 2f;

        /// <summary>차 몸체에서 이 거리(m) 안인데 <see cref="StuckSeconds"/>초 동안 <see cref="StuckMove"/>(m)도 못 움직였으면 막힌 것으로 보고 탄다.
        /// 먼저 와 선 손님이나 차 모서리에 막히면 이동 모듈은 도착도 갇힘도 아닌 채 시간 초과까지 기다린다.</summary>
        private const float StuckNearCar = 3f;

        /// <summary>탑승 지점에서 이 거리(m) 안에서 막혀도 탄다. 지점이 차와 주유기 사이면 주유기에 걸려 차 몸체 3m 바깥에서 설 수 있다.</summary>
        private const float StuckNearTarget = 2.5f;
        private const float StuckSeconds = 2f;
        private const float StuckMove = 0.3f;

        /// <summary>탑승 지점으로 걷되, 가는 도중 차에 충분히 붙으면 바로 멈춘다. 먼저 와 선 손님에 밀리거나
        /// 차 모서리의 작은 NavMesh 조각에 갇히면 목적지 코앞에서 도착도 갇힘도 아닌 채 시간 초과까지 서 있기 때문이다.</summary>
        private async UniTask<VisitOutcome> WalkToCar(Car car, Vector3 target, CancellationToken ct)
        {
            Transform body = Ctx.Customer.transform;
            if (CarLandings.DistanceToBody(car, body.position) <= DoorReach)
            {
                return VisitOutcome.Done;
            }

            // using으로 닫지 않는다. 진 쪽 태스크가 취소를 알아차리는 건 다음 프레임이라, 그 전에 닫으면 닫힌 토큰을 만진다.
            var walk = CancellationTokenSource.CreateLinkedTokenSource(ct);
            UniTask<VisitOutcome> move = MoveAndWait(target, timeout, walk.Token);
            UniTask reached = WaitUntilAtCar(car, body, target, walk.Token);

            try
            {
                (bool moveFinished, VisitOutcome result) = await UniTask.WhenAny(move, reached);
                return moveFinished ? result : VisitOutcome.Done;
            }
            finally
            {
                // 진 쪽을 끊는다. 걷기가 지면 이동 모듈이 finally에서 걷기 애니메이션을 내린다.
                walk.Cancel();
            }
        }

        /// <summary>차에 충분히 붙었거나, 차 근처에서 막혀 더 못 다가갈 때 끝난다.</summary>
        private static async UniTask WaitUntilAtCar(Car car, Transform body, Vector3 target, CancellationToken ct)
        {
            Vector3 lastMovedAt = body.position;
            float lastMoveTime = Time.time;

            while (true)
            {
                if (car == null || body == null)
                {
                    return;
                }

                Vector3 here = body.position;
                float distance = CarLandings.DistanceToBody(car, here);
                if (distance <= DoorReach)
                {
                    return;
                }

                Vector3 moved = here - lastMovedAt;
                moved.y = 0f;
                if (moved.magnitude > StuckMove)
                {
                    lastMovedAt = here;
                    lastMoveTime = Time.time;
                }
                else if (Time.time - lastMoveTime >= StuckSeconds && (distance <= StuckNearCar || PlanarDistance(here, target) <= StuckNearTarget))
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private readonly List<Vector3> _doors = new();

        /// <summary>어디로 걸어가 탈지 고른다. 좌석 쪽으로만 걸어가면, 좌석이 차와 주유기 사이 좁은 틈 쪽일 때
        /// 차 반대편에 붙어 서서도 그 틈이 다른 손님에게 막혀 영영 못 탄다. 탈 때는 좌석으로 붙여 주므로
        /// 차 둘레 후보 중 지금 걸어서 가장 가까운 곳이면 된다. 닿는 후보가 없으면 예전처럼 좌석으로 간다.</summary>
        private Vector3 ChooseBoardingPoint(Car car, Vector3 seat)
        {
            NavMeshAgent agent = Ctx.Customer.Agent;
            if (agent == null)
            {
                return seat;
            }

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            CarLandings.Collect(car, filter, _doors);

            Vector3 best = seat;
            float bestLength = float.MaxValue;
            for (int i = 0; i < _doors.Count; i++)
            {
                float length = WalkLengthTo(_doors[i]);
                if (length < bestLength)
                {
                    bestLength = length;
                    best = _doors[i];
                }
            }

            return best;
        }
    }
}
