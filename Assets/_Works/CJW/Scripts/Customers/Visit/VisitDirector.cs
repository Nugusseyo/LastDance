using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.ManagingAgent;
using _Works.CJW.Scripts.MapSystems;
using DevLib.EventChannelSystem;
using DevLib.ObjectPool.Runtime;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>
    /// 방문을 만들고 끝내는 주체. 풀에서 차와 손님을 꺼내 VisitSession에 넘기고,
    /// 끝난 방문의 등록 해제와 반납까지 책임진다.
    /// </summary>
    public class VisitDirector : MonoBehaviour, IUpdate, IVisitDirector
    {
        private sealed class ActiveVisit
        {
            public VisitSession Session;
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
        [Tooltip("차가 손님 목록을 지정하지 않았을 때 쓰는 기본 손님 종류.")]
        [SerializeField] private CustomerDataSO[] defaultCustomerDataList;

        [Header("경로")]
        [Tooltip("차량이 처음 나타나는 위치.")]
        [SerializeField] private Transform spawnPoint;


        [Tooltip("하차한 손님이 향할 가게 안 위치.")]
        [SerializeField] private Transform shopPoint;
        [Tooltip("방문이 끝난 차량이 빠져나갈 위치.")]
        [SerializeField] private Transform exitPoint;

        [Header("설정")]
        [SerializeField] private float spawnInterval = 8f;
        [SerializeField] private int maxConcurrentVisits = 3;
        [Tooltip("0보다 크면 그 시간 뒤에 자동으로 출발시킨다. 청소 시스템 연결 전 확인용.")]
        [SerializeField] private float autoDepartSeconds;

        private readonly List<ActiveVisit> _activeVisits = new();
        private readonly Stack<VisitSession> _sessionPool = new();
        private readonly List<AbstractCustomer> _spawnBuffer = new();

        private float _spawnTimer;

        public int ActiveVisitCount => _activeVisits.Count;

        /// <summary>
        /// 방문이 시작될 때 발생. 세션이 이미 Arriving 단계이므로 Car와 Customers를 바로 읽을 수 있다.
        /// 청소·주문 쪽에서 이걸 구독해 세션을 들고 있다가 RequestDeparture()를 부르면 된다.
        /// </summary>
        public event Action<VisitSession> VisitStarted;
        private void OnEnable()
        {
            if (!HasValidReferences())
            {
                enabled = false;
                return;
            }

            _spawnTimer = 0f;
            RegisterAgent(this);
        }

        private void OnDisable()
        {
            UnRegisterAgent(this);
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
        }

        /// <summary>
        /// 틱마다 스폰을 할 수 있는지 확인하는 메서드
        /// </summary>
        /// <param name="dt"></param>
        private void TickSpawn(float dt)
        {
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

            _spawnTimer = spawnInterval;
            BeginVisit();
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

        /// <summary>차 한 대와 손님 몇 명을 꺼내 방문을 시작한다.</summary>
        /// <summary>주차 자리를 하나 빌리고, 차 한 대와 손님 몇 명을 꺼내 방문을 시작한다.</summary>
        public VisitSession BeginVisit()
        {
            // 자리부터 잡는다. 풀에서 차를 꺼낸 뒤에 실패하면 되돌릴 것이 늘어난다.
            if (!mapData.TryRentParkingSlot(spawnPoint.position, out RentableMapPosition slot))
            {
                Debug.LogWarning("[VisitDirector] 빈 주차 자리가 없어 방문을 시작하지 못했습니다.", this);
                return null;
            }

            CarDataSO carData = WeightedPicker.Pick(carDataList, data => data.SpawnWeight);
            if (carData == null || carData.PoolItem == null)
            {
                Debug.LogError("[VisitDirector] 뽑을 수 있는 차 데이터가 없습니다. CarDataSO의 풀 항목과 가중치를 확인하세요.", this);
                mapData.ReleaseParkingSlot(slot);
                return null;
            }

            Car car = poolManager.Pop<Car>(carData.PoolItem);
            if (car == null)
            {
                Debug.LogError($"[VisitDirector] 차량을 꺼내지 못했습니다. PoolManager에 {carData.PoolItem.name} 항목이 등록되어 있는지 확인하세요.", this);
                mapData.ReleaseParkingSlot(slot);
                return null;
            }

            car.Setup(carData);
            car.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

            if (!TrySpawnCustomers(car, carData))
            {
                poolManager.Push(car);
                mapData.ReleaseParkingSlot(slot);
                return null;
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
                          shopPoint.position, exitPoint.position);

            VisitStarted?.Invoke(session);

            return session;
        }

        private bool TrySpawnCustomers(Car car, CarDataSO carData)
        {
            _spawnBuffer.Clear();

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

            for (int i = 0; i < count; i++)
            {
                CustomerDataSO customerData = WeightedPicker.Pick(pool, data => data.SpawnWeight);
                if (customerData == null || customerData.PoolItem == null)
                {
                    Debug.LogError("[VisitDirector] 뽑을 수 있는 손님 데이터가 없습니다. CustomerDataSO의 풀 항목과 가중치를 확인하세요.", this);
                    ReturnSpawnBuffer();
                    return false;
                }

                AbstractCustomer customer = poolManager.Pop<AbstractCustomer>(customerData.PoolItem);

                if (customer == null)
                {
                    Debug.LogError($"[VisitDirector] 손님을 꺼내지 못했습니다: {customerData.PoolItem.name}", this);
                    ReturnSpawnBuffer();
                    return false;
                }

                customer.Setup(customerData);

                // 좌석에 붙기 전까지 NavMesh 밖에 서 있지 않도록 차 위치로 옮겨둔다.
                customer.transform.position = car.transform.position;
                _spawnBuffer.Add(customer);
            }

            return true;
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

            // ReturnToPool이 목록을 비우므로 등록 해제를 먼저 끝낸다.
            UnRegisterAgent(session);
            UnRegisterAgent(session.Car);

            IReadOnlyList<AbstractCustomer> customers = session.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                UnRegisterAgent(customers[i]);
            }

            session.ReturnToPool(poolManager);

            for (int i = _activeVisits.Count - 1; i >= 0; i--)
            {
                if (_activeVisits[i].Session != session)
                {
                    continue;
                }

                // 빌린 자리는 반드시 짝을 맞춰 돌려준다.
                mapData.ReleaseParkingSlot(_activeVisits[i].Slot);
                _activeVisits.RemoveAt(i);
                break;
            }

            _sessionPool.Push(session);
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
