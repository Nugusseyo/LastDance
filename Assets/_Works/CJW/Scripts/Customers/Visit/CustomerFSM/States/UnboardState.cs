using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.MapSystems;
using DevLib.SoundSystem;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>차에서 내린다. 좌석 번호만큼 간격을 두고 내려 세션이 한 명씩 처리할 필요가 없다. 어디로 갈지는 다음 상태가 정한다.</summary>
    [Serializable]
    public sealed class UnboardState : CustomerState
    {
        [Tooltip("좌석 번호 × 이 간격만큼 기다린 뒤 내린다. 0이면 차의 BoardingInterval을 쓴다.")]
        [SerializeField, Min(0f)] private float stagger;
        [Tooltip("하차 간격에 더해지는 무작위 흔들림. 0이면 정확히 같은 간격으로 내린다.")]
        [SerializeField, Min(0f)] private float jitter = 0.2f;

        [Header("사운드")]
        [Tooltip("차에서 내릴 때 낼 소리(차 문 여닫는 소리). 내린 자리에서 난다.")]
        [SerializeField] private SoundClipSo unboardSound;


        /// <summary>멈춘 차의 NavMesh 구멍이 반영되기를 기다리는 최소 시간(초).</summary>
        private const float CarveSettleSeconds = 0.5f;

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
                delay += Random.Range(0f, jitter);
            }

            // 차는 멈출 때 NavMesh에 구멍을 다시 내는데, 실제 NavMesh에는 다음 갱신에서야 반영된다.
            // 그 전에 내릴 쪽을 고르면 자기 차 구멍이 없는 NavMesh로 판단해 막힌 쪽에 내리게 된다.
            delay = Mathf.Max(delay, CarveSettleSeconds);

            if (delay > 0f)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            }

            // 고정 시간만으로는 모자랄 때가 있다. 구멍이 실제로 난 것을 보고 나서 고른다.
            await WaitForCarve(visit.Car, ct);

            AbstractCustomer customer = Ctx.Customer;
            if (customer.Boarding == null)
            {
                // 여기서 멈추면 방문 전체가 굳는다. 사실만 남기고 넘어간다.
                Debug.LogError(
                    $"[UnboardState] {customer.name}에 탑승 모듈이 없어 내리지 못했습니다. " +
                    "프리팹에 BoardingModule을 붙여야 합니다.", customer);
                return VisitOutcome.Blocked;
            }

            customer.Boarding.Unboard(ChooseLanding(visit.Car));
            customer.Sound?.Play(unboardSound);
            return VisitOutcome.Done;
        }

        /// <summary>차가 NavMesh에 구멍을 낼 때까지 기다리는 최대 시간(초). 넘기면 구멍 없이 그대로 고른다.</summary>
        private const float CarveWaitLimit = 3f;

        /// <summary>차 한가운데에서 사람 NavMesh가 사라질 때까지 기다린다. 멈춘 차의 NavMeshObstacle은 정지로 판정된 뒤
        /// 다음 갱신에서야 구멍을 내므로, 그 전에 내릴 자리를 고르면 곧 닫힐 길을 열린 길로 보고 막힌 틈에 내린다.</summary>
        private async UniTask WaitForCarve(Car car, CancellationToken ct)
        {
            NavMeshAgent agent = Ctx.Customer.Agent;
            NavMeshObstacle obstacle = car.GetComponentInChildren<NavMeshObstacle>();
            if (agent == null || obstacle == null || !obstacle.enabled || !obstacle.carving)
            {
                return;
            }

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 center = obstacle.transform.TransformPoint(obstacle.center);
            float deadline = Time.time + CarveWaitLimit;

            // 차 한가운데 바로 아래에 NavMesh가 남아 있으면 아직 구멍이 나지 않은 것이다. 차 중심은 바닥보다 높으니
            // 반경을 넉넉히 잡되, 수평으로 가까운 것만 센다 — 구멍이 나면 가장 가까운 NavMesh는 차 옆구리 바깥이다.
            while (HasNavMeshUnder(center, filter) && Time.time < deadline)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (Time.time >= deadline)
            {
                Debug.LogWarning($"[{nameof(UnboardState)}] {car.name}이(가) {CarveWaitLimit}초 안에 NavMesh에 구멍을 내지 않아 그대로 내립니다.", car);
            }
        }

        private static bool HasNavMeshUnder(Vector3 center, NavMeshQueryFilter filter)
        {
            if (!NavMesh.SamplePosition(center, out NavMeshHit hit, 1.5f, filter))
            {
                return false;
            }

            Vector3 delta = hit.position - center;
            delta.y = 0f;
            return delta.sqrMagnitude < 0.5f * 0.5f;
        }

        private readonly List<Vector3> _landings = new();
        private readonly List<int> _reach = new();

        /// <summary>어디에 내릴지 고른다. 줄지어 선 차가 NavMesh를 깎으면 벽이 되어, 차 옆이 다른 차·벽에 둘러싸인
        /// 막힌 틈이 된다. 거기 내리면 어디에도 걸어가지 못한다. 그래서 차 둘레의 여러 자리를 NavMesh 위에 붙여 보고,
        /// 맵의 지점에 가장 많이 닿는(= 막힌 틈이 아닌) 자리만 남긴다. 그중 다음에 걸어갈 지점까지 가장 가까운 자리,
        /// 걸어갈 지점이 없으면 주유기에 가까운 자리를 고른다. NavMesh에 붙인 위치를 그대로 돌려주므로 판단한 곳에 실제로 선다.</summary>
        private Vector3 ChooseLanding(Car car)
        {
            Vector3 dropOff = car.DropOffPosition;
            NavMeshAgent agent = Ctx.Customer.Agent;
            if (agent == null)
            {
                return dropOff;
            }

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            CarLandings.Collect(car, filter, _landings);
            if (_landings.Count == 0)
            {
                return dropOff;
            }

            // 1) 막힌 틈 거르기: 가장 많은 지점에 닿는 후보만 남긴다.
            _reach.Clear();
            int bestReach = -1;
            for (int i = 0; i < _landings.Count; i++)
            {
                int reach = CountReachablePoints(_landings[i]);
                _reach.Add(reach);
                bestReach = Mathf.Max(bestReach, reach);
            }

            for (int i = _landings.Count - 1; i >= 0; i--)
            {
                if (_reach[i] < bestReach)
                {
                    _landings.RemoveAt(i);
                }
            }

            // 2) 먼저 내려 서 있는 사람 자리는 뺀다. 겹쳐 내리면 NavMesh 회피가 둘을 밀어내며 몸과 Agent가 어긋난다.
            //    1)에서 남는 자리가 한두 곳뿐인 일이 많아, 모두 차 있으면 그 옆으로 차를 따라 한두 걸음 비킨 자리를 더 본다.
            //    그래도 모두 차 있으면 가장 덜 붐비는 자리 하나만 남긴다.
            if (AllOccupied(_landings))
            {
                AddSideSteps(car, filter, bestReach, _landings);
            }

            RemoveOccupied(_landings);

            // 3) 다음에 걸어갈 곳까지 가장 가까운 자리. 문에서 먼 자리는 걷는 거리만큼 불리하게 센다.
            MapPointType next = Ctx.Machine != null ? Ctx.Machine.FirstDestination(VisitPhase.Waiting) : MapPointType.None;
            bool hasPump = TryGetPumpPosition(car.transform.position, out Vector3 pump);
            Vector3 best = _landings[0];
            float bestScore = float.MaxValue;

            for (int i = 0; i < _landings.Count; i++)
            {
                Vector3 candidate = _landings[i];
                float score = Vector3.Distance(candidate, dropOff);

                if (next != MapPointType.None)
                {
                    float path = ShortestPathTo(next, candidate);
                    score += path < float.MaxValue ? path : 1000f;
                }
                else if (hasPump)
                {
                    // 3) 걸어갈 곳이 없으면 주유기 쪽(두 주차 줄 사이)이 기본이다.
                    score += Vector3.Distance(candidate, pump);
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>이 거리(m) 안에 다른 사람이 서 있으면 그 자리는 차 있다고 본다. 손님 Agent 지름(1m)에 조금 여유를 둔다.</summary>
        private const float PersonalSpace = 1.2f;

        /// <summary>다른 사람이 서 있는 후보를 뺀다. 모두 차 있으면 가장 가까운 사람이 가장 먼 후보 하나만 남긴다.</summary>
        private void RemoveOccupied(List<Vector3> candidates)
        {
            AbstractCustomer self = Ctx.Customer;
            _clearance.Clear();
            int roomiest = 0;
            int free = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                float clearance = NearestOtherPerson(candidates[i], self);
                _clearance.Add(clearance);
                if (clearance >= PersonalSpace)
                {
                    free++;
                }

                if (clearance > _clearance[roomiest])
                {
                    roomiest = i;
                }
            }

            if (free == 0)
            {
                Vector3 keep = candidates[roomiest];
                candidates.Clear();
                candidates.Add(keep);
                return;
            }

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (_clearance[i] < PersonalSpace)
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        private readonly List<float> _clearance = new();

        private bool AllOccupied(List<Vector3> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (NearestOtherPerson(candidates[i], Ctx.Customer) >= PersonalSpace)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>차를 따라 앞뒤로 비킬 거리(m). 한 사람 몫씩이다.</summary>
        private static readonly float[] SideSteps = { PersonalSpace, -PersonalSpace, PersonalSpace * 2f, -PersonalSpace * 2f };

        private readonly List<Vector3> _extra = new();

        /// <summary>남은 자리마다 차의 앞뒤 방향으로 비킨 자리를 NavMesh에 붙여 더한다. 막힌 틈으로 비키지 않도록
        /// 원래 자리만큼 많은 지점에 닿는 곳만 받는다.</summary>
        private void AddSideSteps(Car car, NavMeshQueryFilter filter, int bestReach, List<Vector3> candidates)
        {
            Vector3 along = car.transform.forward;
            along.y = 0f;
            along.Normalize();

            _extra.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                for (int s = 0; s < SideSteps.Length; s++)
                {
                    Vector3 probe = candidates[i] + along * SideSteps[s];
                    if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 0.5f, filter))
                    {
                        continue;
                    }

                    Vector3 snap = hit.position - probe;
                    snap.y = 0f;
                    if (snap.sqrMagnitude > 0.3f * 0.3f || CountReachablePoints(hit.position) < bestReach)
                    {
                        continue;
                    }

                    _extra.Add(hit.position);
                }
            }

            candidates.AddRange(_extra);
        }

        /// <summary>나를 뺀, 차 밖에 서 있는 가장 가까운 사람까지의 수평 거리(m). 없으면 무한대.</summary>
        private static float NearestOtherPerson(Vector3 point, AbstractCustomer self)
        {
            float nearest = float.PositiveInfinity;
            IReadOnlyList<ICarHittable> people = CarHitTargets.Targets;

            for (int i = 0; i < people.Count; i++)
            {
                ICarHittable person = people[i];
                if (person == null || !person.CanBeHit)
                {
                    continue;
                }

                if (person is Component c && self != null && c.GetComponentInParent<AbstractCustomer>() == self)
                {
                    continue;
                }

                Vector3 d = person.HitPosition - point;
                d.y = 0f;
                nearest = Mathf.Min(nearest, d.magnitude);
            }

            return nearest;
        }

        /// <summary>차에서 가장 가까운 주유 지점. 누가 빌렸는지와 상관없이 본다 — 이 차의 주유 손님이 이미 빌렸을 수 있다.</summary>
        private bool TryGetPumpPosition(Vector3 carPosition, out Vector3 pump)
        {
            pump = default;
            if (Ctx.MapData == null)
            {
                return false;
            }

            IReadOnlyList<MapPosition> points = Ctx.MapData.GetAll(MapPointType.OilDispenser);
            float best = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null)
                {
                    continue;
                }

                float distance = (points[i].Position - carPosition).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    pump = points[i].Position;
                }
            }

            return best < float.MaxValue;
        }
    }
}
