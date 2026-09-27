using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>약속 이름별로 손님 둘을 맺어 주는 등록소. 세션을 가로지르므로 <b>서로 다른 차에서 내린 손님끼리도</b> 짝이 된다 —
    /// <see cref="CustomerContext.Visit"/>의 손님 목록은 같은 차만 보기 때문에 이 역할을 대신할 수 없다.
    /// 빌린 자리와 같은 규율을 따른다: 짝을 지었으면 반드시 <see cref="Leave"/>로 돌려줘야 다음 손님이 짝을 찾는다.</summary>
    [CreateAssetMenu(fileName = "Customer Rendezvous", menuName = "JW/Customers/Customer Rendezvous", order = 2)]
    public class CustomerRendezvousSO : ScriptableObject
    {
        [Tooltip("켜면 정해 둔 지점 대신 구운 NavMesh에서 둘 다 걸어서 닿는 랜덤 위치를 골라 만난다. " +
                 "못 찾으면 아래 지점 종류로 물러난다.")]
        [SerializeField] private bool randomWalkable = true;

        [Tooltip("랜덤 위치는 둘의 중간에서 이 반경(m) 안에서만 고른다. 너무 멀면 걷다가 이동 한계 시간을 넘긴다.")]
        [SerializeField, Min(1f)] private float randomRadius = 20f;

        [Tooltip("랜덤 위치까지 각자 걷는 길이의 상한(m). 길이 빙 돌아가는 곳은 버린다.")]
        [SerializeField, Min(1f)] private float maxWalkLength = 30f;

        [Tooltip("랜덤 위치를 몇 번까지 뽑아 볼지. 모두 조건에 안 맞으면 지점 종류로 물러난다.")]
        [SerializeField, Min(1)] private int randomTries = 60;

        [Tooltip("주차 자리·주유기·입구 지점과 차가 다니는 길(MapDataSo.CarRoutes)에서 이 거리(m) 안은 싸움 위치로 고르지 않는다. " +
                 "싸울 땐 비어 있던 자리에 차가 들어오면 차 사이 틈에 갇혀 걸어 나오지 못하고, 길 위면 지나가는 차에 치인다.")]
        [SerializeField, Min(0f)] private float carZoneClearance = 4f;

        [Tooltip("지금 맵에 있는 차에서 이 거리(m) 안은 고르지 않는다. 차가 움직이며 NavMesh를 깎아 틈을 닫는다.")]
        [SerializeField, Min(0f)] private float carClearance = 4f;

        [Tooltip("NavMesh 가장자리(벽, 차가 파낸 구멍)에서 이 거리(m) 안은 고르지 않는다. 둘이 떨어져 마주 설 자리가 있어야 한다.")]
        [SerializeField, Min(0f)] private float edgeClearance = 1.5f;

        [Tooltip("짝이 모일 지점의 종류. 둘의 중간에서 가장 가까운 지점 하나를 골라 양쪽에 같은 값을 준다. " +
                 "None이면 지점을 찾지 않고 둘의 중간에서 만난다. 랜덤 위치를 켜면 그걸 못 찾았을 때만 쓴다.")]
        [SerializeField] private MapPointType meetAt = MapPointType.FightArea;

        [Tooltip("만날 지점을 NavMesh 위에서 찾을 때 허용할 오차(m). 중간 지점이 길 밖일 때만 쓰인다.")]
        [SerializeField, Min(0f)] private float meetSampleRadius = 3f;

        [Tooltip("마주 설 때 두 몸 중심 사이 거리(m). 주먹이 상대 몸에 닿는 거리다. 손님 Agent 지름(1m)보다 가까워 " +
                 "서로 밀어내므로, MeetUpState가 자리에 선 뒤에는 회피를 끈다. 0이면 한 점에서 만난다.")]
        [SerializeField, Min(0f)] private float standSpacing = 0.65f;

        /// <summary>약속 이름마다 먼저 와서 기다리는 사람 하나.</summary>
        private readonly Dictionary<string, CustomerContext> _waiting = new();

        /// <summary>같은 이름으로 기다리는 짝을 찾는다. 찾으면 양쪽 컨텍스트에 서로를 넣고 true,
        /// 못 찾으면 내가 기다리는 쪽이 되고 false를 돌려준다. 기다리는 쪽은 자기 <see cref="CustomerContext.Partner"/>가
        /// 채워지는지 지켜보면 된다.</summary>
        public bool TryPair(string key, CustomerContext self, out CustomerContext partner)
        {
            partner = null;

            if (self == null || string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (_waiting.TryGetValue(key, out CustomerContext waiting))
            {
                // 내가 이미 기다리는 중이면 나 자신과 짝지을 수는 없다.
                if (ReferenceEquals(waiting, self))
                {
                    return false;
                }

                // 기다리던 쪽이 그 사이 반납됐으면 그 자리를 내가 차지한다.
                if (waiting == null || waiting.Customer == null)
                {
                    _waiting[key] = self;
                    return false;
                }

                _waiting.Remove(key);

                Vector3 meet = ResolveMeetPoint(self, waiting);
                SplitStandPoints(meet, self, waiting, out Vector3 selfPoint, out Vector3 waitingPoint);

                self.Partner = waiting;
                self.MeetPoint = selfPoint;

                waiting.Partner = self;
                waiting.MeetPoint = waitingPoint;

                // 주고받기 차례는 둘 중 먼저 준비된 쪽이 시작 시각을 정한다. 지난 싸움 값이 남지 않게 비운다.
                self.ExchangeStartTime = -1f;
                waiting.ExchangeStartTime = -1f;

                partner = waiting;
                return true;
            }

            _waiting[key] = self;
            return false;
        }

        /// <summary>기다리기를 그만두거나 짝을 놓아준다. 상태의 finally에서 반드시 불러야 한다.</summary>
        public void Leave(string key, CustomerContext self)
        {
            if (self == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(key) &&
                _waiting.TryGetValue(key, out CustomerContext waiting) &&
                ReferenceEquals(waiting, self))
            {
                _waiting.Remove(key);
            }

            // 상대에게도 알린다. 상대는 Partner가 null이 된 걸 보고 빠져나온다.
            CustomerContext partner = self.Partner;
            self.Partner = null;

            if (partner != null && ReferenceEquals(partner.Partner, self))
            {
                partner.Partner = null;
            }
        }

        /// <summary>둘이 모일 자리. 지정한 종류의 지점 중 둘의 중간에서 가장 가까운 하나를 고른다.
        /// 한 번만 계산해 양쪽에 같은 값을 주므로, 각자 "나에게 가까운 곳"을 고르다 서로 다른 데로 가는 일이 없다.</summary>
        private Vector3 ResolveMeetPoint(CustomerContext a, CustomerContext b)
        {
            Vector3 mid = (a.Customer.transform.position + b.Customer.transform.position) * 0.5f;

            // 가까운 곳에 트인 자리가 없으면 한 번 더 넓게 찾는다. 둘의 중간으로 물러나면 주차장 한복판일 수 있다.
            if (randomWalkable && (TryPickRandomWalkable(a, b, mid, randomRadius, out Vector3 random) ||
                                   TryPickRandomWalkable(a, b, mid, randomRadius * 2f, out random)))
            {
                return random;
            }

            if (meetAt != MapPointType.None)
            {
                MapDataSo map = a.MapData != null ? a.MapData : b.MapData;

                if (map != null && map.TryGetNearest(meetAt, mid, out MapPosition point))
                {
                    return point.Position;
                }

                // 싸움터가 없다고 방문을 막지는 않는다. 둘의 중간에서라도 만나게 두고 문제만 남긴다.
                Debug.LogWarning($"[{nameof(CustomerRendezvousSO)}] 맵에 {meetAt} 지점이 없어 둘의 중간에서 만납니다. " +
                                 "씬에 MapPosition을 놓아야 합니다.", this);
            }

            return NavMesh.SamplePosition(mid, out NavMeshHit hit, meetSampleRadius, NavMesh.AllAreas)
                ? hit.position
                : mid;
        }

        /// <summary>만날 지점을 가운데 두고 둘이 <see cref="standSpacing"/>만큼 떨어져 설 자리를 나눈다.
        /// 각자 지금 서 있는 쪽을 먼저 시도해 서로 엇갈려 지나가지 않게 하고, 한쪽이 길 밖이거나 둘 사이가 막혔으면 방향을 돌려 본다.
        /// 끝내 못 찾으면 둘 다 가운데로 보낸다 — 겹쳐 서는 게 싸움이 안 일어나는 것보다 낫다.</summary>
        private void SplitStandPoints(Vector3 meet, CustomerContext self, CustomerContext waiting, out Vector3 selfPoint, out Vector3 waitingPoint)
        {
            selfPoint = meet;
            waitingPoint = meet;

            if (standSpacing <= 0f)
            {
                return;
            }

            Vector3 axis = self.Customer.transform.position - waiting.Customer.transform.position;
            axis.y = 0f;
            axis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.right;

            NavMeshAgent agent = self.Customer.Agent;
            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent != null ? agent.agentTypeID : 0,
                areaMask = agent != null ? agent.areaMask : NavMesh.AllAreas
            };

            float half = standSpacing * 0.5f;
            const float sampleTolerance = 0.3f;

            for (int i = 0; i < StandAngles.Length; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, StandAngles[i], 0f) * axis * half;

                if (!NavMesh.SamplePosition(meet + offset, out NavMeshHit a, sampleTolerance, filter) ||
                    !NavMesh.SamplePosition(meet - offset, out NavMeshHit b, sampleTolerance, filter) ||
                    NavMesh.Raycast(a.position, b.position, out _, filter))
                {
                    continue;
                }

                selfPoint = a.position;
                waitingPoint = b.position;
                return;
            }
        }

        /// <summary>설 자리를 나눌 방향. 지금 서 있는 쪽(0도)부터 조금씩 벌려 본다.</summary>
        private static readonly float[] StandAngles = { 0f, 45f, -45f, 90f, -90f, 135f, -135f };

        private static NavMeshPath _path;

        /// <summary>구운 NavMesh에서 둘의 중간 반경 안의 한 점을 뽑는다. 뽑기는 <see cref="MapDataSo.TryGetRandomPosition"/>에 맡기고,
        /// 여기서는 둘 다 끝까지 닿는 길이 있고 그 길이가 상한 안인지만 거른다.
        /// 주차된 차가 파낸 틈이나 막힌 구석은 길이 끝까지 닿지 않아 걸러진다.</summary>
        private bool TryPickRandomWalkable(CustomerContext a, CustomerContext b, Vector3 mid, float radius, out Vector3 result)
        {
            result = mid;
            NavMeshAgent agentA = a.Customer.Agent;
            NavMeshAgent agentB = b.Customer.Agent;
            MapDataSo map = a.MapData != null ? a.MapData : b.MapData;
            if (map == null || agentA == null || agentB == null || !agentA.isOnNavMesh || !agentB.isOnNavMesh)
            {
                return false;
            }

            _path ??= new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = agentA.agentTypeID, areaMask = agentA.areaMask };

            // 싼 검사부터 한다. 경로 계산은 자리가 트여 있을 때만.
            return map.TryGetRandomPosition(mid, radius, filter, randomTries,
                p => IsOpenGround(map, p, filter)
                     && WalkLength(agentA, p) <= maxWalkLength && WalkLength(agentB, p) <= maxWalkLength,
                out result);
        }

        /// <summary>차가 드나드는 곳에서 떨어져 있고 둘레가 트인 자리인지.</summary>
        private bool IsOpenGround(MapDataSo map, Vector3 p, NavMeshQueryFilter filter)
        {
            for (int i = 0; i < CarZones.Length; i++)
            {
                IReadOnlyList<MapPosition> points = map.GetAll(CarZones[i]);
                if (points == null)
                {
                    continue;
                }

                for (int j = 0; j < points.Count; j++)
                {
                    if (points[j] != null && PlanarDistance(points[j].Position, p) < carZoneClearance)
                    {
                        return false;
                    }
                }
            }

            // 차가 드나드는 길(스폰 → 주차 자리 → 출구, 순회 길). 여기서 싸우면 지나가는 차에 치이고 차도 막힌다.
            if (map.DistanceToCarRoute(p) < carZoneClearance)
            {
                return false;
            }

            IReadOnlyList<ICarTrafficSensor> cars = CarTraffic.Sensors;
            for (int i = 0; i < cars.Count; i++)
            {
                if (cars[i] is Component car && car != null && PlanarDistance(car.transform.position, p) < carClearance)
                {
                    return false;
                }
            }

            return !NavMesh.FindClosestEdge(p, out NavMeshHit edge, filter) || edge.distance >= edgeClearance;
        }

        /// <summary>차가 서거나 지나가는 지점 종류.</summary>
        private static readonly MapPointType[] CarZones = { MapPointType.ParkingSlot, MapPointType.OilDispenser, MapPointType.Entrance };

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>끝까지 닿는 길의 길이. 닿지 않으면 무한대.</summary>
        private static float WalkLength(NavMeshAgent agent, Vector3 to)
        {
            if (!agent.CalculatePath(to, _path) || _path.status != NavMeshPathStatus.PathComplete)
            {
                return float.PositiveInfinity;
            }

            float length = 0f;
            Vector3[] corners = _path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        // ScriptableObject는 플레이 모드를 넘어 값이 남는다. MapDataSo와 같은 이유로 여기서 비운다.
        private void OnEnable() => _waiting.Clear();
        private void OnDisable() => _waiting.Clear();
    }
}
