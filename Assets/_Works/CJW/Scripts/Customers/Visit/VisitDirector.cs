using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.CJW.Scripts.MapSystems;
using _Works.JYG._Scripts.Data_Container.Money;
using DevLib.EventChannelSystem;
using DevLib.ObjectPool.Runtime;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>방문을 만들고 끝내는 주체. 풀에서 차와 손님을 꺼내 VisitSession에 넘기고, 끝난 방문의 등록 해제와 반납까지 책임진다.</summary>
    public class VisitDirector : MonoBehaviour, IUpdate, IVisitDirector
    {
        private sealed class ActiveVisit
        {
            public VisitSession Session;
            /// <summary>빌린 주차 자리. 떠나는 차가 자리를 벗어나 먼저 돌려줬으면 null이다.</summary>
            public RentableMapPosition Slot;
            public float WaitTimer;
        }

        [Header("참조")]
        [Tooltip("틱 등록/해제 요청을 보낼 이벤트 채널. AgentManager가 이걸 구독한다.")]
        [SerializeField] private EventChannelSO agentChannel;
        [SerializeField] private PoolManagerSO poolManager;
        [Tooltip("주차 자리 같은 맵 자원을 빌려주는 에셋. 씬에 무엇이 있는지는 이걸 통해서만 안다.")]
        [SerializeField] private MapDataSo mapData;

        [Header("데이터")]
        [Tooltip("스폰할 차 종류. 인원·간격·속도 같은 개별 수치는 각 CarDataSO 안에 있다.")]
        [SerializeField] private CarDataSO[] carDataList;
        [Tooltip("평판. 이 값으로 차 등급의 해금을 판단한다.")]
        [SerializeField] private IntegerDataContainer reputation;
        [Tooltip("평판별 등급 해금과 등급 가중치. 비워두면 등급 없이 spawnWeight로만 뽑는다.")]
        [SerializeField] private CarGradeTableSO carGradeTable;
        [Tooltip("차가 손님 목록을 지정하지 않았을 때 쓰는 기본 손님 종류.")]
        [SerializeField] private CustomerDataSO[] defaultCustomerDataList;
        [Tooltip("좌석 하나를 뽑을 때 주유를 원하는 손님 쪽에서 뽑을 확률. 나머지는 그 외 손님 쪽에서 뽑는다.\n" +
                 "각 쪽 안에서는 CustomerDataSO의 spawnWeight로 나눈다. 한쪽에 후보가 없으면(이미 주유 손님이 탄 차 등) 다른 쪽에서 뽑는다.")]
        [SerializeField, Range(0f, 1f)] private float fuelCustomerChance = 0.7f;

        [Header("경로")]
        [Tooltip("차량이 처음 나타나는 위치.")]
        [SerializeField] private Transform spawnPoint;


        [Tooltip("하차한 손님이 향할 가게 안 위치.")]
        [SerializeField] private Transform shopPoint;
        [Tooltip("방문이 끝난 차량이 빠져나갈 위치.")]
        [SerializeField] private Transform exitPoint;

        [Header("주차 자리 배정")]
        [Tooltip("자리로 들어가는 직선을 계산할 진입점 거리(m). ArrivingState의 approachDistance와 같은 값이어야 판단이 맞는다.")]
        [SerializeField, Min(0f)] private float approachDistance = ParkingApproach.DefaultDistance;

        [Tooltip("진입 직선 위에 이 반경(m) 안으로 차가 있으면 막힌 자리로 본다.")]
        [SerializeField, Min(0f)] private float approachClearRadius = ParkingApproach.DefaultClearRadius;

        [Tooltip("이미 다른 방문이 빌린 자리가 진입 직선에서 이 거리(m) 안에 있으면 막힌 자리로 본다. " +
                 "그 차가 아직 도착 전이어도 곧 그 자리에 선다. 차 반길이 정도로 둔다.")]
        [SerializeField, Min(0f)] private float occupiedSlotRadius = 2f;

        [Tooltip("떠나는 차가 자리 중심에서 이만큼(m) 멀어지면 자리를 돌려준다. 퇴장길에서 막혀 있어도 빈자리에는 다음 차가 들어온다.\n" +
                 "자리 반길이(2.3m)와 차 반길이(약 2.5m)를 더한 것보다 커야 차 몸체가 자리를 벗어난 뒤에 돌려준다. 0이면 퇴장이 끝날 때 돌려준다.")]
        [SerializeField, Min(0f)] private float slotLeaveDistance = 5.5f;

        [Tooltip("진입점까지 오는 길도 같은 줄 앞쪽 자리를 지난다. 진입점에서 이만큼(m) 더 바깥까지 빌린 자리가 있는지 본다.")]
        [SerializeField, Min(0f)] private float approachCorridorExtra = 9f;

        [Tooltip("ArrivingState의 allowBackIn과 같은 값이어야 한다. 끄면 자리 정면 진입만 막혔는지 본다.")]
        [SerializeField] private bool allowBackIn = ParkingApproach.DefaultAllowBackIn;

        [Header("설정")]
        [SerializeField] private float spawnInterval = 8f;
        [Tooltip("스폰 지점에서 이 반경(m) 안에 차가 있으면 빠질 때까지 스폰을 미룬다. 앞차가 막혀 줄이 스폰 지점까지 밀렸을 때 겹쳐 나오지 않게 한다.")]
        [SerializeField, Min(0f)] private float spawnClearRadius = 3f;
        [SerializeField] private int maxConcurrentVisits = 3;
        [Tooltip("0보다 크면 그 시간 뒤에 자동으로 출발시킨다. 청소 시스템 연결 전 확인용.")]
        [SerializeField] private float autoDepartSeconds;

        [Header("지나가는 차")]
        [Tooltip("켜면 스폰 지점에서 차가 일정 간격으로 계속 나와 도로를 지나 퇴장 지점으로 간다. 그중 enterChance 확률로, 들어올 자리가 있을 때만 주유소에 들어와 방문이 된다.\n" +
                 "끄면 예전처럼 spawnInterval마다 방문 차만 나온다.")]
        [SerializeField] private bool passingTraffic;

        [Tooltip("차가 나오는 간격(초) 범위. 매번 이 안에서 무작위로 고른다.")]
        [SerializeField] private Vector2 passingInterval = new(3f, 6f);

        [Tooltip("나온 차가 주유소에 들어올 확률. 빈 주차 자리가 없거나 동시 방문이 꽉 차면 확률과 상관없이 지나간다.")]
        [SerializeField, Range(0f, 1f)] private float enterChance = 0.3f;

        [Tooltip("지나가는 차가 나눠 달리는 차선 수. 스폰→퇴장 직선을 가운데로 두고 옆으로 나란히 놓는다.")]
        [SerializeField, Min(1)] private int passingLanes = 2;

        [Tooltip("차선 중심 사이 거리(m). 도로 폭 안에 모든 차선이 들어가야 한다(Demo 도로 폭 8m → 4m).")]
        [SerializeField, Min(2f)] private float passingLaneSpacing = 4f;

        [Tooltip("동시에 도로를 지나가는 차의 최대 수. 꽉 차면 지나갈 차는 나오지 않고 들어올 차만 나온다.")]
        [SerializeField, Min(0)] private int maxPassingCars = 6;

        [Tooltip("지나가는 차가 퇴장 지점에서 이 거리(m) 안에 들면 사라진다.")]
        [SerializeField, Min(0f)] private float passingExitRadius = 6f;

        [Tooltip("지나가는 차가 이 시간(초) 안에 퇴장 지점에 닿지 못하면 막힌 것으로 보고 치운다.")]
        [SerializeField, Min(1f)] private float passingMaxLifetime = 90f;

        private PassingTraffic _passing;

        private sealed class AbandonedCar
        {
            public Car Car;
            public RentableMapPosition Slot;
        }

        private readonly List<ActiveVisit> _activeVisits = new();

        /// <summary>버려진 차 — 손님이 다른 차를 훔쳐 떠났거나, 탄 사람이 모두 죽은 차. 치울 때까지 주차 자리를 차지한다.</summary>
        private readonly List<AbandonedCar> _abandonedCars = new();
        private readonly Stack<VisitSession> _sessionPool = new();

        private readonly List<AbstractCustomer> _spawnBuffer = new();

        /// <summary>조건으로 후보를 좁힐 때 쓰는 임시 목록. 스폰마다 새로 할당하지 않으려고 들고 있는다.</summary>
        private readonly List<CustomerDataSO> _pickBuffer = new();
        private readonly List<CustomerDataSO> _otherPickBuffer = new();

        /// <summary>이번 방문에서 이미 태운 특수 역할(<see cref="CustomerRoles.RoleOf"/>로 묶은 값). None(일반 손님)은 역할이 아니므로 여기 들어가지 않고, 여럿 태울 수 있다.</summary>
        private readonly HashSet<CustomerType> _takenRoles = new();

        /// <summary>이번 방문에 주유를 원하는 손님을 이미 태웠는지. 역할과 따로 본다 — <see cref="CustomerRoles.WantsFuel"/> 참고.</summary>
        private bool _fuelTaken;

        /// <summary>이번 차에 탄 손님의 정상/진상. 첫 손님이 정하고, 다음 좌석은 같은 쪽 손님만 뽑는다. None이면 아직 아무도 안 탔다.</summary>
        private Resources.DataBase.Human_Data.HumanType _carHumanType;

        /// <summary>손님 데이터마다 정상/진상 판정을 캐시한다. 프리팹을 뒤지는 일이라 좌석마다 다시 하지 않는다.</summary>
        private readonly Dictionary<CustomerDataSO, Resources.DataBase.Human_Data.HumanType> _humanTypeCache = new();

        /// <summary>앞 차에 탄 짝 손님을 기다리는 종류. 다음 차는 이 손님을 첫 좌석에 태운다.</summary>
        private CustomerDataSO _pendingPartner;

        /// <summary>이번에 태운 손님 중 짝이 필요한 종류(없으면 null)와, 이번 차가 앞 차의 짝을 태웠는지.</summary>
        private CustomerDataSO _spawnedPairStarter;
        private bool _spawnedPartner;

        private float _spawnTimer;

        /// <summary>자리 점수 함수. 스폰마다 람다를 새로 만들지 않으려고 한 번만 묶어둔다.</summary>
        private Func<RentableMapPosition, float> _slotScorer;

        /// <summary>진입 직선이 막히지 않은 자리에 주는 가산점. 거리 점수(수십 m)보다 충분히 커서 항상 먼저 뽑힌다.</summary>
        private const float ClearApproachBonus = 100000f;

        public int ActiveVisitCount => _activeVisits.Count;

        /// <summary>손님이 버리고 간 차의 수. 치울 때까지 그만큼 주차 자리가 줄어 있다.</summary>
        public int AbandonedCarCount => _abandonedCars.Count;

        /// <summary>버려진 차가 늘거나 치워질 때 발생. 인자는 남은 대수다.</summary>
        public event Action<int> AbandonedCarCountChanged;

        /// <summary>방문이 시작될 때 발생. 세션이 이미 Arriving 단계라 Car와 Customers를 바로 읽을 수 있다.</summary>
        public event Action<VisitSession> VisitStarted;
        private void OnEnable()
        {
            if (!HasValidReferences())
            {
                enabled = false;
                return;
            }

            _spawnTimer = 0f;
            _passing ??= new PassingTraffic(ReleasePassingCar);
            RegisterAgent(this);
        }

        private void OnDisable()
        {
            UnRegisterAgent(this);
            _passing?.Clear();
        }

        /// <summary>지점들은 OnEnable에서 등록되므로, 모두 모인 Start에서 차가 다닐 길을 계산해 맵에 알린다.</summary>
        private void Start()
        {
            if (enabled)
            {
                mapData.SetCarRoutes(BuildCarRoutes());
            }
        }

        /// <summary>차가 다니는 길을 차 NavMesh로 계산한다. 스폰 → 각 주차 자리 → 출구, 스폰 → 입구 → 출구.
        /// 순회 길은 넣지 않는다. 주차장을 한 바퀴 둘러 거의 전부를 덮어, 손님이 머물 자리가 멀리 밀려나기 때문이다(순회 차는 드물다).
        /// 주차장이 빈 채로 계산하므로 차가 서 있을 때 돌아가는 길은 빠질 수 있다. 쓰는 쪽이 여유 거리를 둔다.</summary>
        private List<Vector3[]> BuildCarRoutes()
        {
            var routes = new List<Vector3[]>();
            var path = new UnityEngine.AI.NavMeshPath();
            UnityEngine.AI.NavMeshQueryFilter filter = CarNavMesh.Filter;

            void Add(Vector3 from, Vector3 to)
            {
                if (CarNavMesh.SamplePosition(from, out UnityEngine.AI.NavMeshHit a, 3f) &&
                    CarNavMesh.SamplePosition(to, out UnityEngine.AI.NavMeshHit b, 3f) &&
                    UnityEngine.AI.NavMesh.CalculatePath(a.position, b.position, filter, path) &&
                    path.status != UnityEngine.AI.NavMeshPathStatus.PathInvalid)
                {
                    routes.Add(path.corners);
                }
                else
                {
                    // 차 NavMesh로 못 이으면 직선이라도 남긴다. 아무것도 없으면 그 길 위에 손님이 설 수 있다.
                    routes.Add(new[] { from, to });
                }
            }

            foreach (MapPosition slot in mapData.GetAll(MapPointType.ParkingSlot))
            {
                Add(spawnPoint.position, slot.Position);
                Add(slot.Position, exitPoint.position);
            }

            foreach (MapPosition entrance in mapData.GetAll(MapPointType.Entrance))
            {
                Add(spawnPoint.position, entrance.Position);
                Add(entrance.Position, exitPoint.position);
            }

            // 지나가는 차가 달리는 도로. 넣지 않으면 손님이 그 위에 머물 자리를 잡는다.
            if (passingTraffic)
            {
                Add(spawnPoint.position, exitPoint.position);
            }

            return routes;
        }

        /// <summary>틱 대상 등록을 이벤트 채널로 요청한다.</summary>
        private void RegisterAgent(object target)
        {
            if (agentChannel == null || target == null)
                return;

            agentChannel.RaiseEvent(AgentEvents.RegisterAgentEvent.Init(target));
        }

        /// <summary>틱 대상 해제를 이벤트 채널로 요청한다.</summary>
        private void UnRegisterAgent(object target)
        {
            if (agentChannel == null || target == null)
                return;

            agentChannel.RaiseEvent(AgentEvents.UnRegisterAgentEvent.Init(target));
        }

        public void OnUpdate(float dt)
        {
            TickSpawn(dt);
            TickAutoDeparture(dt);
            TickSlotRelease();
        }

        /// <summary>떠나는 차가 자리를 벗어나면 방문이 끝나기 전에 자리를 돌려준다. 퇴장 지점에 닿아야 돌려주면
        /// 퇴장길에서 앞차에 막힌 동안(최대 수 분) 빈자리를 붙들고 있어 다음 차가 못 들어온다.</summary>
        private void TickSlotRelease()
        {
            if (slotLeaveDistance <= 0f)
            {
                return;
            }

            for (int i = 0; i < _activeVisits.Count; i++)
            {
                ActiveVisit visit = _activeVisits[i];
                Car car = visit.Session.Car;

                // 버려진 차는 자리에 남아 계속 차지하니 퇴장하는 차만 본다.
                if (visit.Slot == null || visit.Session.Phase != VisitPhase.Leaving || car == null)
                {
                    continue;
                }

                Vector3 delta = car.transform.position - visit.Slot.Position;
                delta.y = 0f;
                if (delta.sqrMagnitude < slotLeaveDistance * slotLeaveDistance)
                {
                    continue;
                }

                mapData.ReleaseParkingSlot(visit.Slot);
                visit.Slot = null;
            }
        }

        /// <summary>틱마다 스폰할 수 있는지 확인한다.</summary>
        private void TickSpawn(float dt)
        {
            if (passingTraffic)
            {
                TickPassingTraffic(dt);
                return;
            }

            // 최대를 넘으면 return
            if (_activeVisits.Count >= maxConcurrentVisits)
                return;

            // 스폰 타이머를 점점 줄임
            _spawnTimer -= dt;
            if (_spawnTimer > 0f)
                return;

            // 자리가 없으면 타이머를 소모하지 않는다. 자리가 나는 순간 바로 스폰된다.
            if (!mapData.HasFreeParkingSlot)
                return;

            // 스폰 지점이 막혀 있어도 마찬가지로 타이머를 남겨둔다.
            if (!CarTraffic.IsAreaClear(spawnPoint.position, spawnClearRadius))
                return;

            // 빈 자리가 있어도 가는 길이 막혀 있거나 다른 차의 진입로 위라면 보내지 않는다. 보내면 누군가 도중에 선다.
            if (!HasGoodFreeSlot())
                return;

            _spawnTimer = spawnInterval;
            BeginVisit();
        }

        /// <summary>도로에 차를 계속 흘려보내고, 그중 몇 대만 주유소에 들인다. 들어올 차는 방문으로, 나머지는 지나가는 차로 나온다.
        /// 앞 차에 탄 싸움꾼의 짝은 확률과 상관없이 들어온다 — 안 들이면 먼저 온 싸움꾼이 혼자 기다린다.</summary>
        private void TickPassingTraffic(float dt)
        {
            _passing.Tick(dt, passingExitRadius, passingMaxLifetime);

            _spawnTimer -= dt;
            if (_spawnTimer > 0f)
                return;

            // 들어올지는 차 한 대마다 한 번만 정한다. 스폰 자리가 막혀 기다리는 동안 매 프레임 다시 뽑으면 들어오는 확률이 부풀어 오른다.
            _nextSpawnEnters ??= CanEnterStation() && (_pendingPartner != null || Random.value < enterChance);

            if (_nextSpawnEnters.Value)
            {
                // 방문 차는 도로 가운데 스폰 지점에서 나온다. 막혀 있으면 빠질 때까지 기다린다.
                if (!CarTraffic.IsAreaClear(spawnPoint.position, spawnClearRadius))
                    return;

                // 기다리는 사이 자리가 찼으면 다시 정한다.
                if (!CanEnterStation())
                {
                    _nextSpawnEnters = null;
                    return;
                }

                // BeginVisit이 짝을 곧바로 보내려고 타이머를 0으로 되돌릴 수 있으므로 먼저 건다.
                _nextSpawnEnters = null;
                _spawnTimer = NextPassingInterval();
                BeginVisit();
                return;
            }

            if (_passing.Count >= maxPassingCars)
            {
                _nextSpawnEnters = null;
                _spawnTimer = NextPassingInterval();
                return;
            }

            // 비어 있는 차선이 생길 때까지 기다린다.
            if (!TryPickPassingLane(out Vector3 laneFrom, out Vector3 laneTo))
                return;

            _nextSpawnEnters = null;
            _spawnTimer = NextPassingInterval();
            SpawnPassingCar(laneFrom, laneTo);
        }

        /// <summary>다음 차가 들어올 차인지. 아직 정하지 않았으면 null.</summary>
        private bool? _nextSpawnEnters;

        /// <summary>지나가는 차 차선의 시작점을 비었다고 볼 반경(m). 옆 차선 차(중심 간격 passingLaneSpacing)는 걸리지 않고 같은 차선 앞차만 걸리게 작게 둔다.</summary>
        private const float PassingLaneClearRadius = 2f;

        private float NextPassingInterval() => Random.Range(passingInterval.x, Mathf.Max(passingInterval.x, passingInterval.y));

        /// <summary>시작점이 비어 있는 차선을 무작위 순서로 하나 고른다. 차선은 스폰→퇴장 직선을 옆으로 나란히 옮긴 것이다.</summary>
        private bool TryPickPassingLane(out Vector3 from, out Vector3 to)
        {
            from = to = default;

            Vector3 direction = exitPoint.position - spawnPoint.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1f)
            {
                return false;
            }

            Vector3 right = Vector3.Cross(Vector3.up, direction.normalized);
            int lanes = Mathf.Max(1, passingLanes);
            int first = Random.Range(0, lanes);

            for (int k = 0; k < lanes; k++)
            {
                int lane = (first + k) % lanes;
                Vector3 offset = right * ((lane - (lanes - 1) * 0.5f) * passingLaneSpacing);
                if (!CarTraffic.IsAreaClear(spawnPoint.position + offset, PassingLaneClearRadius))
                {
                    continue;
                }

                from = spawnPoint.position + offset;
                to = exitPoint.position + offset;
                return true;
            }

            return false;
        }

        /// <summary>지금 차 한 대를 주유소에 들일 수 있는지. 동시 방문이 남고, 가는 길이 막히지 않은 빈 자리가 있어야 한다.</summary>
        private bool CanEnterStation()
        {
            return _activeVisits.Count < maxConcurrentVisits && mapData.HasFreeParkingSlot && HasGoodFreeSlot();
        }

        /// <summary>손님 없이 도로만 지나갈 차를 스폰 지점에 꺼내 퇴장 지점으로 보낸다. 겉모습은 평판으로 등급을 뽑아 고른다.</summary>
        private void SpawnPassingCar(Vector3 from, Vector3 to)
        {
            CarDataSO data = PickGradedCar();
            if (data == null || data.PoolItem == null)
            {
                return;
            }

            Car car = poolManager.Pop<Car>(data.PoolItem);
            if (car == null)
            {
                Debug.LogError($"[VisitDirector] 지나가는 차를 꺼내지 못했습니다. PoolManager에 {data.PoolItem.name} 항목이 등록되어 있는지 확인하세요.", this);
                return;
            }

            car.Setup(data);
            Vector3 heading = to - from;
            heading.y = 0f;
            car.transform.SetPositionAndRotation(from, Quaternion.LookRotation(heading));

            // 틱 등록은 바퀴를 굴리려고 한다. 이동 모듈은 목적지가 없어 가만히 있고, 차는 PassingTraffic이 도로를 따라 옮긴다.
            RegisterAgent(car);
            _passing.Add(car, from, to);
        }

        private void ReleasePassingCar(Car car)
        {
            car.SetGliding(false);
            UnRegisterAgent(car);
            poolManager.Push(car);
        }

        private void TickAutoDeparture(float dt)
        {
            if (autoDepartSeconds <= 0f)
                return;

            // 완료된 방문이 순회 도중 빠질 수 있으므로 뒤에서부터 훑는다.
            for (int i = _activeVisits.Count - 1; i >= 0; i--)
            {
                ActiveVisit visit = _activeVisits[i];
                if (visit.Session.Phase != VisitPhase.Waiting)
                {
                    visit.WaitTimer = 0f;
                    continue;
                }

                visit.WaitTimer += dt;
                if (visit.WaitTimer >= autoDepartSeconds)
                {
                    visit.Session.RequestDeparture();
                }
            }
        }

        /// <summary>퇴치를 눈으로 확인하기 위한 임시 진입점. 무엇이 퇴치를 부를지 정해지면
        /// PlayerInteractModule 쪽에서 이벤트로 넘어오게 바꾸고 이건 지운다.</summary>
        [ContextMenu("디버그: 첫 방문 퇴치")]
        private void DebugRepelFirst()
        {
            if (_activeVisits.Count == 0)
            {
                Debug.Log("[VisitDirector] 진행 중인 방문이 없습니다.", this);
                return;
            }

            VisitSession session = _activeVisits[0].Session;
            Debug.Log($"[VisitDirector] {session.Car?.name}의 방문을 {session.Phase} 단계에서 퇴치합니다.", this);
            session.Repel();
        }

        /// <summary>주차 자리를 하나 빌리고, 차 한 대와 손님 몇 명을 꺼내 방문을 시작한다.</summary>
        public VisitSession BeginVisit()
        {
            // 자리부터 잡는다. 풀에서 차를 꺼낸 뒤에 실패하면 되돌릴 것이 늘어난다.
            _slotScorer ??= ScoreSlot;
            if (!mapData.TryRentParkingSlot(_slotScorer, out RentableMapPosition slot))
            {
                Debug.LogWarning("[VisitDirector] 빈 주차 자리가 없어 방문을 시작하지 못했습니다.", this);
                return null;
            }

            // 짝을 기다리는 손님이 있으면 그 손님을 태울 수 있는 차 종류만 뽑는다.
            CarDataSO carData = PickCarData(AvailableCarData(_pendingPartner));
            if (carData == null || carData.PoolItem == null)
            {
                Debug.LogError("[VisitDirector] 뽑을 수 있는 차 데이터가 없습니다. CarDataSO의 풀 항목과 가중치를 확인하세요.", this);
                mapData.ReleaseParkingSlot(slot);
                return null;
            }

            CarDataSO visualData = PickVisual(carData);
            PoolItemSO visual = visualData.PoolItem;
            Car car = poolManager.Pop<Car>(visual);
            if (car == null)
            {
                Debug.LogError($"[VisitDirector] 차량을 꺼내지 못했습니다. PoolManager에 {visual.name} 항목이 등록되어 있는지 확인하세요.", this);
                mapData.ReleaseParkingSlot(slot);
                return null;
            }

            car.Setup(carData);
            car.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            // 바퀴를 바닥에 맞추는 건 이동 모듈이 출발(MoveTo)할 때 한다. 스폰 지점의 높이는 신경 쓰지 않아도 된다.

            if (!TrySpawnCustomers(car, carData))
            {
                poolManager.Push(car);
                mapData.ReleaseParkingSlot(slot);
                return null;
            }

            // 손님 체력은 눈에 보이는 차의 등급을 따른다. randomVisual 차는 행동 등급(Low)이 아니라 빌려 온 겉모습의 등급.
            ApplyGradeHealth(visualData.Grade);

            // 한 차에는 한쪽 손님만 탄다. 차만 보고도 정상/진상 차를 알 수 있게 넘겨 둔다.
            car.SetHumanType(_carHumanType);

            // 짝으로 오는 손님을 태웠으면 다음 차에 짝을 태워 곧바로 보낸다. 짝을 태운 차였다면 기다림을 끝낸다.
            if (_spawnedPartner)
            {
                _pendingPartner = null;
            }
            else if (_spawnedPairStarter != null)
            {
                _pendingPartner = _spawnedPairStarter;
                _spawnTimer = 0f;
            }

            VisitSession session = RentSession();
            session.Completed += OnVisitCompleted;

            RegisterAgent(car);
            for (int i = 0; i < _spawnBuffer.Count; i++)
            {
                RegisterAgent(_spawnBuffer[i]);
            }
            RegisterAgent(session);

            _activeVisits.Add(new ActiveVisit { Session = session, Slot = slot });

            session.Begin(car, _spawnBuffer,
                          slot.Position, slot.Rotation,
                          shopPoint.position, exitPoint.position, mapData);

            VisitStarted?.Invoke(session);

            return session;
        }

        private readonly List<CarDataSO> _carPickBuffer = new();
        private readonly List<CarDataSO> _gradePickBuffer = new();
        private Func<CarGrade, bool> _hasGradeCandidate;

        /// <summary>평판으로 해금된 등급을 가중치로 먼저 뽑고, 그 등급 안에서 spawnWeight로 차를 뽑는다.
        /// 등급 표·평판이 없거나 뽑을 등급 있는 차가 없으면 후보 전체에서 spawnWeight로 뽑는다.</summary>
        private CarDataSO PickCarData(List<CarDataSO> candidates)
        {
            if (carGradeTable != null && reputation != null)
            {
                _hasGradeCandidate ??= grade => HasGrade(_carPickBuffer, grade);

                if (carGradeTable.TryPickGrade(reputation.Value, _hasGradeCandidate, out CarGrade grade))
                {
                    _gradePickBuffer.Clear();
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        if (candidates[i].Grade == grade)
                        {
                            _gradePickBuffer.Add(candidates[i]);
                        }
                    }

                    return WeightedPicker.Pick(_gradePickBuffer, data => data.SpawnWeight);
                }
            }

            return WeightedPicker.Pick(candidates, data => data.SpawnWeight);
        }

        private readonly List<CarDataSO> _visualBuffer = new();
        private readonly List<CarDataSO> _visualGradeBuffer = new();
        private Func<CarGrade, bool> _hasVisualCandidate;

        /// <summary>이번 차에 태운 손님들의 최대 체력을 등급 표에서 정한다. 표가 없거나 등급이 없으면 프리팹 체력 그대로.</summary>
        private void ApplyGradeHealth(CarGrade grade)
        {
            if (carGradeTable == null || !carGradeTable.TryGetCustomerHealth(grade, out float health))
            {
                return;
            }

            for (int i = 0; i < _spawnBuffer.Count; i++)
            {
                _spawnBuffer[i].Health?.SetMaxHealth(health);
            }
        }

        /// <summary>이 방문에 쓸 차 겉모습을 가진 차 데이터. randomVisual인 차는 등급 있는 차들의 겉모습 중에서
        /// 평판으로 등급을 뽑고 그 안에서 spawnWeight로 고른다. 동시 대수 제한은 행동(CarDataSO) 기준이라 여기서는 보지 않는다.</summary>
        private CarDataSO PickVisual(CarDataSO carData)
        {
            if (!carData.RandomVisual)
            {
                return carData;
            }

            CarDataSO picked = PickGradedCar();
            return picked != null && picked.PoolItem != null ? picked : carData;
        }

        /// <summary>등급 있는 차 중에서 평판으로 등급을 뽑고 그 안에서 spawnWeight로 하나 고른다.
        /// 겉모습만 빌려 쓰는 차(randomVisual)와 도로를 지나가는 차가 쓴다. 고를 차가 없으면 null.</summary>
        private CarDataSO PickGradedCar()
        {
            _visualBuffer.Clear();
            for (int i = 0; i < carDataList.Length; i++)
            {
                CarDataSO data = carDataList[i];
                if (data != null && !data.RandomVisual && data.Grade != CarGrade.None && data.PoolItem != null)
                {
                    _visualBuffer.Add(data);
                }
            }

            CarDataSO picked = null;
            if (carGradeTable != null && reputation != null)
            {
                _hasVisualCandidate ??= grade => HasGrade(_visualBuffer, grade);

                if (carGradeTable.TryPickGrade(reputation.Value, _hasVisualCandidate, out CarGrade grade))
                {
                    _visualGradeBuffer.Clear();
                    for (int i = 0; i < _visualBuffer.Count; i++)
                    {
                        if (_visualBuffer[i].Grade == grade)
                        {
                            _visualGradeBuffer.Add(_visualBuffer[i]);
                        }
                    }

                    picked = WeightedPicker.Pick(_visualGradeBuffer, data => data.SpawnWeight);
                }
            }
            else
            {
                picked = WeightedPicker.Pick(_visualBuffer, data => data.SpawnWeight);
            }

            return picked;
        }

        private static bool HasGrade(List<CarDataSO> candidates, CarGrade grade)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                // spawnWeight 0은 뽑히지 않아야 하므로 후보로 치지 않는다. 치면 WeightedPicker가 균등 추첨으로 물러나 뽑혀 버린다.
                if (candidates[i].Grade == grade && candidates[i].SpawnWeight > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>동시 대수 제한(CarDataSO.MaxConcurrent)에 걸리지 않은 차 종류만 추린다.
        /// mustCarry가 있으면 그 손님을 태울 수 있는 차 종류만 남긴다.</summary>
        private List<CarDataSO> AvailableCarData(CustomerDataSO mustCarry = null)
        {
            _carPickBuffer.Clear();

            for (int i = 0; i < carDataList.Length; i++)
            {
                CarDataSO data = carDataList[i];
                if (data == null)
                {
                    continue;
                }

                if (data.MaxConcurrent > 0 && CountActive(data) >= data.MaxConcurrent)
                {
                    continue;
                }

                if (mustCarry != null && Array.IndexOf(data.Customers ?? defaultCustomerDataList, mustCarry) < 0)
                {
                    continue;
                }

                _carPickBuffer.Add(data);
            }

            return _carPickBuffer;
        }

        private int CountActive(CarDataSO data)
        {
            int count = 0;
            for (int i = 0; i < _activeVisits.Count; i++)
            {
                Car car = _activeVisits[i].Session.Car;
                if (car != null && car.Data == data)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>자리 점수. 진입 직선이 막히지 않은 자리가 먼저고, 그중에서는 입구에서 먼(안쪽) 자리가 먼저다.
        /// 입구 쪽 자리부터 채우면 뒤에 온 차가 그 차를 지나 안쪽으로 들어가야 해서, 자리 앞 직선에서 서로 막힌다.
        /// 스폰 지점이 아니라 입구를 기준으로 삼는 이유는, 차가 스폰된 뒤 도로를 돌아 입구로 들어오는 맵에서는
        /// 스폰에서 먼 자리가 오히려 입구 바로 앞일 수 있기 때문이다.</summary>
        private float ScoreSlot(RentableMapPosition slot)
        {
            Vector3 delta = slot.Position - EntryPosition(slot.Position);
            delta.y = 0f;

            float score = delta.magnitude;

            if (IsGoodSlot(slot))
            {
                score += ClearApproachBonus;
            }

            return score;
        }

        /// <summary>차가 주차장에 들어서는 곳. 맵에 입구 지점이 있으면 그걸 쓰고, 없으면 스폰 지점으로 물러선다.</summary>
        private Vector3 EntryPosition(Vector3 from)
        {
            return mapData.TryGetNearest(MapPointType.Entrance, from, out MapPosition entrance)
                ? entrance.Position
                : spawnPoint.position;
        }

        /// <summary>지금 이 자리에 차를 보내도 아무도 멈춰 세우지 않는지. 진입 직선이 비어 있고,
        /// 아직 들어오는 중인 다른 차가 이 자리를 지나가야 하는 상황도 아니어야 한다.</summary>
        private bool IsGoodSlot(RentableMapPosition slot) => HasClearApproach(slot) && !IsOnArrivingPath(slot);

        /// <summary>빈 자리 중 좋은 자리가 하나라도 있는지. 없으면 스폰을 미룬다 — 억지로 보내면 다른 차 앞에서 서게 된다.</summary>
        private bool HasGoodFreeSlot()
        {
            IReadOnlyList<MapPosition> slots = mapData.GetAll(MapPointType.ParkingSlot);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] is RentableMapPosition slot && slot.IsAvailable && IsGoodSlot(slot))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>아직 자리로 들어오는 중인 방문의 진입 직선 위에 이 자리가 놓였는지.
        /// 한 줄로 늘어선 자리에서는 안쪽 자리로 가는 차가 바깥 자리를 지나가야 하므로, 그 차가 들어갈 때까지 바깥 자리를 비워 둔다.</summary>
        private bool IsOnArrivingPath(RentableMapPosition slot)
        {
            for (int i = 0; i < _activeVisits.Count; i++)
            {
                ActiveVisit visit = _activeVisits[i];
                if (visit.Session.Phase != VisitPhase.Arriving || visit.Slot == null)
                {
                    continue;
                }

                // 입구를 막거나 도는 차는 빌린 자리로 가지 않는다.
                if (visit.Session.Car != null && visit.Session.Car.Data != null && visit.Session.Car.Data.StateOverrides != null)
                {
                    continue;
                }

                if (!ParkingApproach.TryGetPoint(visit.Slot.Position, ParkingApproach.ForwardIn(visit.Slot.Rotation),
                                                 approachDistance, ParkingApproach.DefaultSampleRadius, out Vector3 approach))
                {
                    continue;
                }

                if (CarTraffic.PlanarDistanceToSegment(slot.Position, Corridor(approach, visit.Slot.Position), visit.Slot.Position) < occupiedSlotRadius)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>앞쪽·뒤쪽 진입 중 하나라도 직선이 비어 있는지. 서 있는 차와, 이미 빌려 가서 곧 차가 설 자리를 모두 본다.</summary>
        private bool HasClearApproach(RentableMapPosition slot)
        {
            return IsApproachClear(slot, ParkingApproach.ForwardIn(slot.Rotation))
                   || (allowBackIn && IsApproachClear(slot, ParkingApproach.BackIn(slot.Rotation)));
        }

        private bool IsApproachClear(RentableMapPosition slot, Quaternion rotation)
        {
            if (!ParkingApproach.TryGetPoint(slot.Position, rotation, approachDistance,
                                             ParkingApproach.DefaultSampleRadius, out Vector3 approach))
            {
                return false;
            }

            if (!ParkingApproach.IsLegClear(approach, slot.Position, approachClearRadius))
            {
                return false;
            }

            IReadOnlyList<MapPosition> slots = mapData.GetAll(MapPointType.ParkingSlot);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] is not RentableMapPosition other || other == slot || !other.IsOccupied)
                {
                    continue;
                }

                if (CarTraffic.PlanarDistanceToSegment(other.Position, Corridor(approach, slot.Position), slot.Position) < occupiedSlotRadius)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>진입점에서 자리 반대쪽으로 approachCorridorExtra만큼 더 늘린 점. 진입점까지 오는 길도 같은 줄 앞쪽을 지나므로 함께 본다.</summary>
        private Vector3 Corridor(Vector3 approach, Vector3 slotPosition)
        {
            Vector3 dir = approach - slotPosition;
            dir.y = 0f;
            return dir.sqrMagnitude < 1e-6f ? approach : approach + dir.normalized * approachCorridorExtra;
        }

        private bool TrySpawnCustomers(Car car, CarDataSO carData)
        {
            _spawnBuffer.Clear();
            _takenRoles.Clear();
            _fuelTaken = false;
            _carHumanType = Resources.DataBase.Human_Data.HumanType.None;

            // 차가 자기 손님 목록을 들고 있으면 그쪽이 우선. 없으면 디렉터의 기본 목록을 쓴다.
            CustomerDataSO[] pool = carData.Customers ?? defaultCustomerDataList;
            if (pool == null || pool.Length == 0)
            {
                Debug.LogError($"[VisitDirector] {carData.name}에 태울 손님 종류가 없습니다. 차 데이터나 기본 손님 목록을 채우세요.", this);
                return false;
            }

            // 인원 범위는 차의 좌석 수에서 바로 나온다. 좌석을 넘는 값이 생길 수 없다.
            Vector2Int customerRange = car.CustomerCountRange;
            if (customerRange.y <= 0)
            {
                Debug.LogError($"[VisitDirector] {car.name}에 좌석이 없어 방문을 만들 수 없습니다. 프리팹의 Seats 배열을 확인하세요.", this);
                return false;
            }

            int count = Random.Range(customerRange.x, customerRange.y + 1);
            _spawnedPairStarter = null;
            _spawnedPartner = false;

            // 앞 차의 짝이 기다리고 있으면 첫 좌석에 그 짝을 태운다. 추첨하지 않으니 가중치와 상관없이 반드시 탄다.
            if (_pendingPartner != null)
            {
                if (!SpawnIntoBuffer(car, _pendingPartner))
                {
                    ReturnSpawnBuffer();
                    return false;
                }

                _spawnedPartner = true;
                MarkRoleTaken(_pendingPartner);
            }

            // 좌석마다 독립으로 뽑으면 한 차의 구성이 통제되지 않는다. 이미 태운 사람을 보고 후보를 좁힌다.
            while (_spawnBuffer.Count < count)
            {
                CustomerDataSO customerData = PickForSeat(pool);

                if (customerData == null)
                {
                    // 조건을 만족하는 후보가 더 없다. 억지로 태우는 대신 인원을 줄여 끝낸다.
                    break;
                }

                if (!SpawnIntoBuffer(car, customerData))
                {
                    ReturnSpawnBuffer();
                    return false;
                }

                if (customerData.ComesInPairs)
                {
                    _spawnedPairStarter = customerData;
                }

                MarkRoleTaken(customerData);

                // 혼자 오는 손님을 태웠으면 더 태우지 않는다.
                if (customerData.RidesAlone)
                {
                    break;
                }
            }

            if (_spawnBuffer.Count == 0)
            {
                Debug.LogError($"[VisitDirector] {carData.name}의 손님 후보가 조건을 하나도 만족하지 못해 아무도 태우지 못했습니다.", this);
                return false;
            }

            DropExtraFuelCustomers(carData);
            return true;
        }

        /// <summary>마지막 안전장치. 추첨에서 이미 거르지만, 어떤 경로로든 주유 손님이 둘 이상 탔으면 첫 사람만 남기고 풀로 돌린다.
        /// 한 차에 주유 손님이 둘이면 주유기 하나를 두고 둘이 기다리는 그림이 되어 기획과 어긋난다.</summary>
        private void DropExtraFuelCustomers(CarDataSO carData)
        {
            bool kept = false;

            for (int i = 0; i < _spawnBuffer.Count; i++)
            {
                AbstractCustomer customer = _spawnBuffer[i];
                if (!CustomerRoles.WantsFuel(customer.Data))
                {
                    continue;
                }

                if (!kept)
                {
                    kept = true;
                    continue;
                }

                Debug.LogError($"[VisitDirector] {carData.name}에 주유 손님이 둘 이상 탈 뻔해 {customer.Data.name}을(를) 내렸습니다. " +
                               "추첨 조건을 거치지 않은 경로가 있는지 확인해야 합니다.", this);
                poolManager.Push(customer);
                _spawnBuffer.RemoveAt(i);
                i--;
            }
        }

        /// <summary>None(일반 손님)은 역할이 아니라서 중복 허용. 특수 역할만 한 번 태우면 다음 좌석 후보에서 제외한다.</summary>
        private void MarkRoleTaken(CustomerDataSO customerData)
        {
            CustomerType role = CustomerRoles.RoleOf(customerData.CustomerType);
            if (role != CustomerType.None)
            {
                _takenRoles.Add(role);
            }

            if (CustomerRoles.WantsFuel(customerData))
            {
                _fuelTaken = true;
            }

            // 첫 손님이 이 차를 정상 차로 할지 진상 차로 할지 정한다.
            if (_carHumanType == Resources.DataBase.Human_Data.HumanType.None)
            {
                _carHumanType = HumanTypeOf(customerData);
            }
        }

        /// <summary>손님 데이터가 정상인지 진상인지. 평판 판정(VisitReputationReporter)과 같게 리뷰 글 번호를 HumanDB에서 찾아 쓴다 —
        /// 번호가 여럿인데 타입이 갈리거나 번호가 없으면 프리팹의 HumanType을 쓴다. 프리팹을 못 찾으면 None(어느 차에나 탐).</summary>
        private Resources.DataBase.Human_Data.HumanType HumanTypeOf(CustomerDataSO data)
        {
            if (_humanTypeCache.TryGetValue(data, out Resources.DataBase.Human_Data.HumanType cached))
            {
                return cached;
            }

            var type = Resources.DataBase.Human_Data.HumanType.None;
            GameObject prefab = data.PoolItem != null ? data.PoolItem.prefab : null;
            AbstractCustomer customer = prefab != null ? prefab.GetComponentInChildren<AbstractCustomer>(true) : null;
            if (customer != null)
            {
                type = customer.HumanType;
                int[] reviews = customer.ReviewIndices;
                if (reviews is { Length: > 0 })
                {
                    var first = HumanTypeTable.Of(reviews[0], customer.HumanType);
                    bool same = true;
                    for (int i = 1; i < reviews.Length && same; i++)
                    {
                        same = HumanTypeTable.Of(reviews[i], customer.HumanType) == first;
                    }

                    if (same)
                    {
                        type = first;
                    }
                }
            }

            _humanTypeCache[data] = type;
            return type;
        }

        /// <summary>손님 하나를 풀에서 꺼내 태울 목록에 넣는다. 꺼내지 못하면 false.</summary>
        private bool SpawnIntoBuffer(Car car, CustomerDataSO customerData)
        {
            if (customerData.PoolItem == null)
            {
                Debug.LogError("[VisitDirector] 뽑을 수 있는 손님 데이터가 없습니다. CustomerDataSO의 풀 항목과 가중치를 확인하세요.", this);
                return false;
            }

            AbstractCustomer customer = poolManager.Pop<AbstractCustomer>(customerData.PoolItem);

            if (customer == null)
            {
                Debug.LogError($"[VisitDirector] 손님을 꺼내지 못했습니다: {customerData.PoolItem.name}", this);
                return false;
            }

            customer.Setup(customerData);

            // 좌석에 붙기 전까지 NavMesh 밖에 서 있지 않도록 차 위치로 옮겨둔다.
            customer.transform.position = car.transform.position;
            _spawnBuffer.Add(customer);
            return true;
        }

        /// <summary>좌석 하나를 채울 손님을 뽑는다. 이미 태운 역할(None 제외)은 후보에서 빼고 추첨한다.
        /// 이렇게 하면 한 차 안에서 같은 역할(예: 주유 손님)이 둘 이상 겹치는 일이 없다.
        /// 조건을 만족하는 후보가 없으면 null을 돌려주고, 부르는 쪽이 인원을 줄인다.</summary>
        private CustomerDataSO PickForSeat(CustomerDataSO[] pool)
        {
            // 이미 태운 역할의 가중치만 0으로 만들면 안 된다. 후보가 전부 그 역할이면 합이 0이 되어
            // WeightedPicker가 균등 추첨으로 물러나고, 결국 걸러내려던 손님을 돌려준다.
            // 목록에서 아예 빼야 조건이 지켜진다.
            _pickBuffer.Clear();

            for (int i = 0; i < pool.Length; i++)
            {
                CustomerDataSO data = pool[i];
                if (data == null)
                {
                    continue;
                }

                // 종류가 아니라 역할로 거른다. 주유 손님(내리는 쪽·차에 남는 쪽)은 한 차에 하나만 탄다.
                CustomerType role = CustomerRoles.RoleOf(data.CustomerType);
                if (role != CustomerType.None && _takenRoles.Contains(role))
                {
                    continue;
                }

                // 주유를 원하는 손님은 종류가 달라도 한 차에 하나만 탄다.
                if (_fuelTaken && CustomerRoles.WantsFuel(data))
                {
                    continue;
                }

                // 한 차에는 정상 손님만, 또는 진상 손님만 탄다. 첫 손님과 다른 쪽은 후보에서 뺀다.
                if (_carHumanType != Resources.DataBase.Human_Data.HumanType.None)
                {
                    var type = HumanTypeOf(data);
                    if (type != Resources.DataBase.Human_Data.HumanType.None && type != _carHumanType)
                    {
                        continue;
                    }
                }

                // 혼자 오는 손님은 첫 좌석에서만 뽑힌다. 이미 누가 탔으면 후보에서 뺀다.
                if (data.RidesAlone && _spawnBuffer.Count > 0)
                {
                    continue;
                }

                // 짝으로 오는 손님은 짝을 태운 차가 곧바로 뒤따라올 수 있을 때만 뽑힌다.
                // 이미 짝을 기다리는 중이거나 이 차 다음에 빈 자리·방문 여유가 없으면 혼자 남게 된다.
                if (data.ComesInPairs && !CanSendPartner(data))
                {
                    continue;
                }

                _pickBuffer.Add(data);
            }

            return WeightedPicker.Pick(PickGroup(), data => data.SpawnWeight);
        }

        /// <summary>후보를 주유 손님과 그 외로 나눠 <see cref="fuelCustomerChance"/>로 한쪽을 고른다.
        /// 한쪽이 비었으면 다른 쪽을 돌려준다. <see cref="_pickBuffer"/>에는 주유 손님만 남는다.</summary>
        private List<CustomerDataSO> PickGroup()
        {
            _otherPickBuffer.Clear();

            for (int i = _pickBuffer.Count - 1; i >= 0; i--)
            {
                if (!CustomerRoles.WantsFuel(_pickBuffer[i]))
                {
                    _otherPickBuffer.Add(_pickBuffer[i]);
                    _pickBuffer.RemoveAt(i);
                }
            }

            // 가중치 합이 0인 쪽(모두 spawnWeight 0)을 고르면 WeightedPicker가 균등 추첨으로 물러나 뽑히면 안 될 손님이 나온다. 빈 쪽으로 본다.
            if (TotalWeight(_pickBuffer) <= 0f)
            {
                return _otherPickBuffer;
            }

            if (TotalWeight(_otherPickBuffer) <= 0f)
            {
                return _pickBuffer;
            }

            return Random.value < fuelCustomerChance ? _pickBuffer : _otherPickBuffer;
        }

        private static float TotalWeight(List<CustomerDataSO> list)
        {
            float sum = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                sum += Mathf.Max(0f, list[i].SpawnWeight);
            }

            return sum;
        }

        /// <summary>지금 뽑는 차 뒤로 짝을 태운 차 한 대를 더 보낼 수 있는지. 지금 차는 자리를 이미 빌렸고 방문 목록에는 아직 없다.</summary>
        private bool CanSendPartner(CustomerDataSO partner)
        {
            return _pendingPartner == null &&
                   _activeVisits.Count + 2 <= maxConcurrentVisits &&
                   mapData.AvailableCountOf(MapPointType.ParkingSlot) >= 1 &&
                   AvailableCarData(partner).Count > 0;
        }

        private void ReturnSpawnBuffer()
        {
            for (int i = 0; i < _spawnBuffer.Count; i++)
            {
                poolManager.Push(_spawnBuffer[i]);
            }

            _spawnBuffer.Clear();
        }

        private void OnVisitCompleted(VisitSession session)
        {
            session.Completed -= OnVisitCompleted;

            // ReturnToPool이 세션을 비우므로 필요한 값을 먼저 읽는다.
            bool abandoned = session.IsCarAbandoned;
            Car car = session.Car;
            StealableCar stolen = session.StolenCar;

            // ReturnToPool이 목록을 비우므로 등록 해제를 먼저 끝낸다.
            // 버려진 차도 더는 움직이지 않으니 틱에서 뺀다. 차 자체는 자리에 남아 다른 차가 피해 간다.
            UnRegisterAgent(session);
            UnRegisterAgent(car);

            IReadOnlyList<AbstractCustomer> customers = session.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                UnRegisterAgent(customers[i]);
            }

            session.ReturnToPool(poolManager);

            // 훔친 차는 손님을 풀로 돌린 다음에 치운다. 먼저 끄면 좌석에 앉은 손님까지 함께 꺼진다.
            if (stolen != null)
            {
                stolen.Vanish();
            }

            for (int i = _activeVisits.Count - 1; i >= 0; i--)
            {
                if (_activeVisits[i].Session != session)
                {
                    continue;
                }

                if (abandoned)
                {
                    // 버려진 차가 자리를 계속 차지한다. 자리는 차를 치울 때 돌려준다.
                    _abandonedCars.Add(new AbandonedCar { Car = car, Slot = _activeVisits[i].Slot });

                    // 팀원 쪽 치우기(IRemovableCar.Remove)가 이 차를 찾으면 여기로 돌아온다.
                    car.SetRemover(ClearAbandonedCar);
                    AbandonedCarCountChanged?.Invoke(_abandonedCars.Count);
                }
                else if (_activeVisits[i].Slot != null)
                {
                    // 빌린 자리는 반드시 짝을 맞춰 돌려준다. 떠나며 자리를 벗어났을 때 이미 돌려줬으면 null이다.
                    mapData.ReleaseParkingSlot(_activeVisits[i].Slot);
                }

                _activeVisits.RemoveAt(i);
                break;
            }

            _sessionPool.Push(session);
        }

        /// <summary>버려진 차 중 하나를 고른다. 치우는 상호작용이 생기면 플레이어가 가리킨 차를 넘기면 된다.</summary>
        public bool TryGetAbandonedCar(int index, out Car car)
        {
            car = index >= 0 && index < _abandonedCars.Count ? _abandonedCars[index].Car : null;
            return car != null;
        }

        /// <summary>버려진 차를 치운다. 차를 풀로 돌려보내고 차지하던 주차 자리를 비운다. 버려진 차가 아니면 false.</summary>
        public bool ClearAbandonedCar(Car car)
        {
            for (int i = 0; i < _abandonedCars.Count; i++)
            {
                if (_abandonedCars[i].Car != car)
                {
                    continue;
                }

                car.SetRemover(null);
                if (_abandonedCars[i].Slot != null)
                {
                    mapData.ReleaseParkingSlot(_abandonedCars[i].Slot);
                }
                poolManager.Push(car);
                _abandonedCars.RemoveAt(i);
                AbandonedCarCountChanged?.Invoke(_abandonedCars.Count);
                return true;
            }

            return false;
        }

        private VisitSession RentSession() => _sessionPool.Count > 0 ? _sessionPool.Pop() : new VisitSession();

        private bool HasValidReferences()
        {
            if (agentChannel == null || poolManager == null || mapData == null)
            {
                Debug.LogError("[VisitDirector] 이벤트 채널(EventChannelSO)과 PoolManager, MapData를 모두 지정해야 합니다.", this);
                return false;
            }

            if (carDataList == null || carDataList.Length == 0)
            {
                Debug.LogError("[VisitDirector] 차 데이터(CarDataSO)를 하나 이상 지정해야 합니다.", this);
                return false;
            }

            if (spawnPoint == null || shopPoint == null || exitPoint == null)
            {
                Debug.LogError("[VisitDirector] 스폰·가게·퇴장 지점을 모두 지정해야 합니다. 정차 위치는 ParkingSlot이 대신합니다.", this);
                return false;
            }

            return true;
        }
    }
}
