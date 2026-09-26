using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.ManagingAgents;
using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>NavMeshAgent를 경로 계산기로만 쓰고, 이동은 Pure Pursuit + 자전거 모델로 직접 굴리는 차량 이동 모듈. 에이전트에 이동을 맡기면 제자리 회전까지 해버려 차처럼 보이지 않기 때문이다.</summary>
    public partial class CarSteeringMoveModule : AbstractModule, ICarMoveModule, IUpdate
    {
        [Header("참조")]
        [SerializeField] private NavMeshAgent _agent;

        [Header("도착 판정")]
        [Tooltip("stoppingDistance가 이보다 작으면 이 값을 도착 판정에 쓴다.")]
        [SerializeField] private float _arriveThreshold = 0.5f;

        [Tooltip("자리에 들어왔다고 인정할 실제 거리(m). 종방향만 보면 옆으로 밀린 채로 도착으로 친다. " +
                 "너무 작게 잡으면 멀집한 주차도 재시도로 밀린다.")]
        [SerializeField, Min(0.2f)] private float _arrivalRadius = 1f;

        [Tooltip("자리에 못 붙었을 때 다시 돌아 들어올 횟수. 다 쓰면 경고를 남기고 그 자리에서 멈춴다.")]
        [SerializeField, Min(0)] private int _maxApproachRetries = 2;

        [Tooltip("회피 우선순위. 낮을수록 먼저다. 손님(기본 50)보다 낮게 두어야 차가 손님을 피하지 않는다.")]
        [SerializeField] private int _avoidancePriority;

        [Header("차체 (자전거 모델)")]
        [Tooltip("앞축과 뒷축 사이 거리(m). 회전 반경의 기준이다. 이 오브젝트의 피벗은 뒷축에 있는 편이 자연스럽다.")]
        [SerializeField, Min(0.1f)] private float _wheelBase = 2f;

        [Tooltip("최대 조향각(도). 최소 회전 반경 = 축거 / tan(이 각). 작을수록 크게 도는 큰 차가 된다.")]
        [SerializeField, Range(5f, 70f)] private float _maxSteerAngle = 45f;

        [Tooltip("핸들이 꾺이는 속도(도/초). 낮을수록 코너 진입이 완만해진다.")]
        [SerializeField, Min(1f)] private float _steerRate = 140f;

        [Header("속도")]
        [Tooltip("가속도(m/s²). 최고 속도는 NavMeshAgent.Speed를 쓴다.")]
        [SerializeField, Min(0.1f)] private float _accel = 6f;

        [Tooltip("감속도(m/s²). 가속보다 크게 두는 편이 자연스럽다.")]
        [SerializeField, Min(0.1f)] private float _brakeAccel = 7f;

        [Tooltip("허용 횡가속도(m/s²). 코너에서 얼마나 속도를 줄일지 정한다. 4~6이 얄전한 승용차.")]
        [SerializeField, Min(0.5f)] private float _maxLateralAccel = 5f;

        [Header("Pure Pursuit")]
        [Tooltip("Lookahead = 속도 × 이 값. 클수록 멀리 보고 부드럽게 가지만 코너를 크게 자른다.")]
        [SerializeField, Min(0f)] private float _lookaheadGain = 1f;

        [Tooltip("Lookahead 하한(m). 몇 m 앞에서 돌기 시작하느냐와 같다. 너무 작으면 좌우로 떨린다.")]
        [SerializeField, Min(0.1f)] private float _lookaheadMin = 3f;

        [Tooltip("Lookahead 상한(m).")]
        [SerializeField, Min(0.1f)] private float _lookaheadMax = 8f;

        [Header("경로")]
        [Tooltip("경로를 다시 읽는 주기(초). NavMesh가 경로를 갈아끼워도 이 주기 안에 따라잡는다.")]
        [SerializeField, Min(0.05f)] private float _pathRefreshInterval = 0.4f;

        [Tooltip("경유지에 이만큼 가까워지면 마지막 직선 구간으로 넘어간다(m). Lookahead보다 크게 두어야 되돌아가지 않는다.")]
        [SerializeField, Min(0.5f)] private float _viaSwitchDistance = 4f;

        [Header("마지막 직선 구간")]
        [Tooltip("정차 자리로 들어갈 때의 Lookahead 상한(m). 짧을수록 선에 밀착해 좌우 오차가 줄어든다. " +
                 "짧게 하고 싶으면 Final Leg Speed도 같이 낮춰야 흔들리지 않는다.")]
        [SerializeField, Min(0.5f)] private float _finalLegLookahead = 2.5f;

        [Tooltip("정차 자리로 들어갈 때의 최고 속도(m/s). 주차는 천천히 해야 자연스럽고 자리도 정확하게 들어간다.")]
        [SerializeField, Min(0.2f)] private float _finalLegSpeed = 3.5f;

        /// <summary>경로 코너와 목표점 찾기를 담당한다.</summary>
        private readonly CarPathTracker _path = new CarPathTracker();

        [Header("후진")]
        [Tooltip("후진 최고 속도(m/s). 전진보다 느려야 자연스럽다.")]
        [SerializeField, Min(0.2f)] private float _reverseSpeed = 2f;

        [Tooltip("한 번의 후진에서 물러날 수 있는 최대 거리(m). 여기까지 오면 무조건 다시 전진한다.")]
        [SerializeField, Min(0.5f)] private float _maxReverseDistance = 6f;

        [Tooltip("최소 후진 거리(m). 이만큼은 물러난 뒤에 전진으로 돌아간다. 앞뒤로 떨리는 것을 막는다.")]
        [SerializeField, Min(0f)] private float _minReverseDistance = 1.2f;

        [Tooltip("뒤쪽을 확인할 거리(m). 이 안에서 NavMesh가 끊기면 후진을 멈춘다.")]
        [SerializeField, Min(0.5f)] private float _reverseClearance = 2.5f;

        [Header("차간 거리")]
        [Tooltip("앞차와 이만큼은 떨어져 선다(m). ICarTrafficSensor 모듈이 있을 때만 쓴다.")]
        [SerializeField, Min(0f)] private float _minGap = 1.5f;

        [Tooltip("미리 알고 줄이는 감속(앞차·목적지·진입 구간·코너)에 쓰는 감속도(m/s²). _brakeAccel보다 작게 두어 멀리서부터 부드럽게 줄인다. " +
                 "_brakeAccel은 이걸로는 못 설 때만 쓴다.")]
        [SerializeField, Min(0.5f)] private float _comfortBrakeAccel = 3f;

        [Tooltip("앞차를 살피는 최소 거리(m). 속도가 빠르면 제동거리만큼 자동으로 늘어난다.")]
        [SerializeField, Min(0.5f)] private float _sensorRange = 6f;

        /// <summary>없으면 차간 거리를 보지 않는다. 예전처럼 다른 차를 무시하고 달린다.</summary>
        private ICarTrafficSensor _traffic;

        [Header("바닥")]
        [Tooltip("차 높이를 맞출 바닥 콜라이더 레이어. 도로(Road)와 바닥(Plane)이 있는 Ground 레이어.")]
        [SerializeField] private LayerMask _groundMask = 1 << 7;

        [Tooltip("피벗을 바닥에서 띄울 높이(m). 0보다 작으면 모델의 가장 낮은 곳(바퀴 바닥)을 재서 자동으로 정한다.")]
        [SerializeField] private float _rideHeightOverride = -1f;

        private const float GroundProbeUp = 2f;
        private const float GroundProbeDown = 4f;

        /// <summary>피벗을 바닥에서 띄우는 높이. 바퀴가 바닥에 닿도록 초기화할 때 잰다.</summary>
        private float _rideHeight;

        /// <summary>차체 크기. 후진할 때 뒤 범퍼 너머부터 NavMesh를 보려고 쓴다. 없으면 축거로 어림한다.</summary>
        private NavMeshObstacle _footprint;

        /// <summary>이번 프레임에 겨눈 목표점 쪽(월드, 수평). 센서가 차로를 이 방향으로 긋는다.</summary>
        private Vector3 _travelDir;

        [Tooltip("앞차에 막혀 이 시간(초) 동안 서 있고, 그 차도 나를 기다리는 교착이면 우선순위가 낮은 쪽이 물러선다.")]
        [SerializeField, Min(0.1f)] private float _backoffDelay = 1.5f;

        [Tooltip("교착이 아니어도 이 시간(초) 넘게 막혀 있으면 물러섰다가 다른 각도로 다시 비켜 본다.")]
        [SerializeField, Min(0.1f)] private float _backoffStuckTime = 6f;

        [Tooltip("교착을 풀 때 물러설 거리(m).")]
        [SerializeField, Min(0.5f)] private float _backoffDistance = 3f;

        /// <summary>뒤차에 막혀 물러서지도 못할 때 영영 후진 기어에 머물지 않게 하는 한계(초).</summary>
        private const float BackoffTimeout = 3f;

        /// <summary>서 있다 출발할 때 경로 계산을 미루는 프레임 수. 자기 NavMeshObstacle 구멍이 사라지길 기다린다.</summary>
        private const int PendingStartFrames = 2;

        private int _pendingFrames;
        private Vector3 _pendingStart;

        /// <summary>이번 프레임에 최대 제동(_brakeAccel)이 필요한지.</summary>
        private bool _hardBrake;

        private float _blockedTime;
        private float _backoffRemaining;
        private float _backoffTimer;

        private bool _hasDestination;

        /// <summary>후진 중인지. 기어를 바꾸려면 먼저 속도가 0이 되어야 한다.</summary>
        private bool _reversing;

        /// <summary>이번 후진에서 물러난 거리(m).</summary>
        private float _reverseTravelled;
        private float _speed;

        /// <summary>현재 조향각(라디안). 곱률이 아니라 각도로 들고 있어야 한계와 변화율을 물리적으로 자를 수 있다.</summary>
        private float _steer;

        private float _remaining = float.PositiveInfinity;

        /// <summary>차에서 진짜 목적지까지의 실제 수평 거리. 경로가 아니라 직선 거리다.</summary>
        private float _endDistance = float.PositiveInfinity;

        /// <summary>진짜 목적지. 진입점을 거쳐 가는 동안에도 이건 자리 좌표다.</summary>
        private Vector3 _destination;

        private Vector3 _approachFrom;
        private bool _hasApproach;

        /// <summary>지금까지 다시 들어온 횟수.</summary>
        private int _retryCount;

        /// <summary>재시도를 다 썼다. 더 버티면 방문이 끝나지 않으므로 이 자리를 도착으로 친다.</summary>
        private bool _giveUp;

        /// <summary>도착으로 칠 실제 거리. 종방향 판정보다 느슨하게 둔다.</summary>
        private float ArrivalRadius => Mathf.Max(ArriveDistance, _arrivalRadius);

        /// <summary>현재 속도(m/s). 바퀴 회전이나 엔진음에 쓰면 된다.</summary>
        public float Speed => _speed;

        /// <summary>후진 중인지. 후진등이나 경고음에 쓰면 된다.</summary>
        public bool IsReversing => _reversing;

        /// <summary>현재 조향각(도). 앞바퀴를 실제로 돌려 보여줄 때 쓴다.</summary>
        public float SteerAngleDeg => _steer * Mathf.Rad2Deg;

        /// <summary>최소 회전 반경(m). 자전거 모델에서 나오는 이 차의 물리적 한계다.</summary>
        public float MinTurnRadius => CarSteeringSolver.MinTurnRadius(_wheelBase, _maxSteerAngle);

        /// <summary>목적지까지 실제로 닿는 경로인지. SetDestination은 부분 경로에도 true를 돌려주므로 따로 확인해야 한다.</summary>
        public bool HasCompletePath => !_hasDestination || _path.Status == NavMeshPathStatus.PathComplete;

        private float ArriveDistance => Mathf.Max(_arriveThreshold, _agent != null ? _agent.stoppingDistance : 0f);

        public bool IsArrived
        {
            get
            {
                if (!_hasDestination)
                {
                    return true;
                }

                if (_agent.pathPending)
                {
                    return false;
                }

                // 재시도를 다 썼으면 여기가 끝이다. 아니면 방문이 영영 끝나지 않는다.
                if (_giveUp)
                {
                    return true;
                }

                // remaining은 차를 선에 투영한 점 기준이라, 옆으로 4m 벗어나 있어도 0이 된다.
                // 실제 거리를 함께 봐야 엉또한 자리에 서고 "도착"이라 보고하지 않는다.
                return _remaining <= ArriveDistance && _endDistance <= ArrivalRadius;
            }
        }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _traffic = owner.GetModule<ICarTrafficSensor>();
            _footprint = GetComponentInChildren<NavMeshObstacle>(true);
            _rideHeight = MeasureRideHeight();

            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            if (_agent == null)
            {
                Debug.LogError($"[CarSteering] {name}에 NavMeshAgent가 없습니다.", this);
                return;
            }

            _agent.avoidancePriority = _avoidancePriority;

            // 에이전트는 경로만 계산한다. 위치와 회전은 이 모듈이 직접 만든다.
            _agent.updatePosition = false;
            _agent.updateRotation = false;
        }

        public void ApplyStats(float moveSpeed, float arriveThreshold)
        {
            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            if (moveSpeed > 0f)
            {
                _agent.speed = moveSpeed;
            }

            if (arriveThreshold > 0f)
            {
                _arriveThreshold = arriveThreshold;
            }
        }

        public void MoveTo(Vector3 destination)
        {
            _hasApproach = false;
            _destination = destination;
            _retryCount = 0;
            _giveUp = false;
            _path.ClearApproach();
            MoveToInternal(destination);
        }

        /// <summary>approachFrom을 먼저 지나 destination에 닿는다. 하나의 연속된 경로라 중간에 멈추지 않는다.</summary>
        public void MoveTo(Vector3 destination, Vector3 approachFrom)
        {
            _hasApproach = true;
            _destination = destination;
            _approachFrom = approachFrom;
            _retryCount = 0;
            _giveUp = false;
            _path.SetApproach(destination, approachFrom);
            MoveToInternal(approachFrom);
        }

        private void MoveToInternal(Vector3 destination)
        {
            if (_agent == null)
            {
                return;
            }

            // 풀에서 꺼낸 차는 풀 위치(차 NavMesh 밖일 수 있음)에서 켜져 에이전트가 NavMesh에 붙지 못한 채로 온다.
            // 지금 자리로 옮겨 붙여 본 뒤에도 안 되면 그때 포기한다.
            if (!_agent.isOnNavMesh)
            {
                _agent.Warp(transform.position);
            }

            if (!_agent.isOnNavMesh)
            {
                Debug.LogWarning($"[CarSteering] {name}이(가) 차 NavMesh 위에 없어 이동할 수 없습니다. 위치 {transform.position}", this);
                _hasDestination = false;
                return;
            }

            // 달리는 동안에는 자기 NavMeshObstacle이 발밑에 구멍을 내지 않게 한다. 구멍 안에서 경로를 뽑으면
            // 시작점이 구멍 가장자리(대개 차 뒤쪽)로 밀려, 주차했다 출발하는 차가 괜히 후진부터 한다.
            // 구멍은 다음 NavMesh 갱신 때 사라지므로, 서 있다 출발하는 경우엔 경로 계산을 두 프레임 미룬다.
            // 경로 계산을 미루더라도 바퀴 높이는 지금 맞춘다. 스폰 지점에 놓인 차가 그 사이 떠 있거나 파묻혀 보이지 않게.
            SnapToGround();

            bool wasCarving = _footprint != null && _footprint.carving;
            if (_footprint != null)
            {
                _footprint.carving = false;
            }

            if (wasCarving && Mathf.Abs(_speed) < 0.1f)
            {
                _pendingStart = destination;
                _pendingFrames = PendingStartFrames;
                _hasDestination = true;
                _path.BeginPath();
                _remaining = float.PositiveInfinity;
                _endDistance = float.PositiveInfinity;
                return;
            }

            StartPath(destination);
        }

        /// <summary>경로를 실제로 뽑아 출발한다.</summary>
        private void StartPath(Vector3 destination)
        {
            _pendingFrames = 0;

            // 풀에서 꺼낸 차는 풀 위치(차 NavMesh 밖일 수 있음)에서 켜져 에이전트가 NavMesh에 붙지 못한 채로 온다.
            // 지금 자리로 옮겨 붙여 본 뒤에도 안 되면 그때 포기한다.
            if (!_agent.isOnNavMesh)
            {
                _agent.Warp(transform.position);
            }

            if (!_agent.isOnNavMesh)
            {
                Debug.LogWarning($"[CarSteering] {name}이(가) 차 NavMesh 위에 없어 이동할 수 없습니다. 위치 {transform.position}", this);
                _hasDestination = false;
                return;
            }

            // 풀에서 꺼내 스폰 지점에 놓인 차는 바닥에서 떠 있거나 파묻혀 있을 수 있다. 출발 전에 바퀴를 바닥에 맞춘다.
            SnapToGround();

            // 정차 중이나 풀에서 꺼낸 직후에는 에이전트 내부 위치가 실제 차와 어긋나 있을 수 있다.
            // 경로를 여기부터 뽑게 먼저 맞춰준다.
            _agent.Warp(transform.position);
            _agent.isStopped = false;

            _hasDestination = _agent.SetDestination(destination);

            if (!_hasDestination)
            {
                Debug.LogWarning($"[CarSteering] {name}의 목적지 {destination}까지 경로를 찾지 못했습니다.", this);
                return;
            }

            _path.BeginPath();

            // 이전 목적지에서 하던 후진을 새 목적지까지 끌고 가지 않는다.
            _reversing = false;
            _reverseTravelled = 0f;
            _backoffRemaining = 0f;
            _blockedTime = 0f;

            _remaining = float.PositiveInfinity;
            _endDistance = float.PositiveInfinity;
        }

        public void Stop()
        {
            _hasDestination = false;
            _speed = 0f;
            _steer = 0f;
            _reversing = false;
            _reverseTravelled = 0f;
            _backoffRemaining = 0f;
            _blockedTime = 0f;
            _retryCount = 0;
            _giveUp = false;
            _remaining = float.PositiveInfinity;
            _endDistance = float.PositiveInfinity;
            _path.Clear();
            _traffic?.CancelBypass();
            _pendingFrames = 0;

            // 선 차는 다시 구멍을 내 손님과 다른 차의 경로가 이 차를 돌아가게 한다.
            if (_footprint != null)
            {
                _footprint.carving = true;
            }

            if (_agent == null || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;

            // updatePosition/updateRotation은 계속 false다. 손님이 옆에서 내려도 차가 밀리지 않고,
            // 에이전트는 켜져 있으니 손님들은 여전히 차를 피해 간다.
        }

        /// <summary>매 프레임의 순서. 자세한 계산은 아래 메서드들에 나눠둔다.</summary>
        public void OnUpdate(float dt)
        {
            if (!_hasDestination || _agent == null || dt <= 0f)
            {
                return;
            }

            if (_pendingFrames > 0)
            {
                if (--_pendingFrames > 0)
                {
                    return;
                }

                StartPath(_pendingStart);
                if (!_hasDestination)
                {
                    return;
                }
            }

            _endDistance = CarPathTracker.HorizontalDistance(transform.position, _destination);

            _path.TrySwitchToFinalLeg(transform.position, transform.forward, _viaSwitchDistance, _agent);
            _path.RefreshIfNeeded(_agent, dt, _pathRefreshInterval);

            // 따라갈 선분이 없거나 다 왔으면 핸들을 풀면서 세운다.
            if (!_path.HasPath)
            {
                Settle(dt);
                return;
            }

            Vector3 cursor = _path.Advance(transform.position, out _remaining);

            if (_remaining <= ArriveDistance)
            {
                Settle(dt);

                // 선을 다 썼는데 자리에서는 멀다. 옆으로 밀린 채로 끝점을 지난 것이다.
                // 완전히 선 뒤에 다시 들어온다.
                if (!_giveUp && _endDistance > ArrivalRadius && Mathf.Abs(_speed) <= 0.01f)
                {
                    RetryApproach();
                }

                return;
            }

            Vector3 local = ResolveGoal(cursor);            // 1. 어디를 겫는가
            bool shifting = UpdateGear(local, dt);          // 2. 전진인가 후진인가
            float curvature = UpdateSteering(local, dt);    // 3. 핸들을 얼마나 꾫는가
            UpdateSpeed(curvature, dt, shifting);           // 4. 얼마나 밟는가
            Integrate(curvature, cursor.y, dt);             // 5. 실제로 움직인다
        }

        /// <summary>자리에 못 붙은 채 끝점을 지났을 때 처음부터 다시 들어온다. 횟수를 제한하지 않으면 자리 앞이 막혔을 때 영원히 재시도해 방문이 끝나지 않는다.</summary>
        private void RetryApproach()
        {
            _retryCount++;

            if (_retryCount > _maxApproachRetries)
            {
                Debug.LogWarning(
                    $"[CarSteering] {name}이(가) {_maxApproachRetries}번 시도하고도 자리에 붙지 못해 이 자리에서 멈춥니다. " +
                    $"(목적지까지 {_endDistance:F1}m) 진입 공간이나 최소 회전 반경을 확인하세요.", this);

                _giveUp = true;
                return;
            }

            if (_hasApproach)
            {
                _path.SetApproach(_destination, _approachFrom);
                MoveToInternal(_approachFrom);
            }
            else
            {
                _path.ClearApproach();
                MoveToInternal(_destination);
            }
        }

        /// <summary>전진과 후진 중 어느 쪽인지 정한다. 기어를 바꾸는 중이면 true를 돌려준다. 후진은 경로를 따라가는 동작이 아니라 각을 벌기 위한 동작이라, 전진으로 목표에 닿을 수 없을 때만 들어간다.</summary>
        private bool UpdateGear(Vector3 local, float dt)
        {
            bool want;

            if (_backoffRemaining > 0f)
            {
                // 교착을 풀려고 물러서는 중. 정해진 거리를 다 물러서거나 뒤가 막히면 그만둔다.
                _backoffTimer += dt;
                if (_reversing)
                {
                    _backoffRemaining -= Mathf.Abs(_speed) * dt;
                    _reverseTravelled += Mathf.Abs(_speed) * dt;
                }

                if (IsReverseBlocked() || _backoffTimer > BackoffTimeout)
                {
                    _backoffRemaining = 0f;
                }

                want = _backoffRemaining > 0f;
            }
            else if (!_reversing)
            {
                // 핸들을 끝까지 꾫어도 못 닿는다. 물러나야 한다.
                // 다만 뒤가 막혔으면 후진으로 바꿔 봐야 곧바로 다시 전진으로 돌아와, 기어만 바꾸며 제자리에 선다.
                // 그때는 핸들을 끝까지 꺾은 채 전진해 크게 돌아 들어온다.
                want = !CarSteeringSolver.CanReachForward(local, MinTurnRadius) && !IsReverseBlocked();
            }
            else
            {
                _reverseTravelled += Mathf.Abs(_speed) * dt;

                bool enough = _reverseTravelled >= _minReverseDistance;
                bool reachable = CarSteeringSolver.CanReachForward(local, MinTurnRadius);

                // 너무 많이 물러났거나 뒤가 막혔으면, 각이 안 나와도 일단 전진한다.
                // 안 그러면 무한으로 물러난다.
                bool giveUp = _reverseTravelled >= _maxReverseDistance || IsReverseBlocked();

                want = !(giveUp || (enough && reachable));
            }

            if (want == _reversing)
            {
                return false;
            }

            if (Mathf.Abs(_speed) > 0.05f)
            {
                return true;   // 아직 굴러가는 중. 이번 프레임은 제동만 한다.
            }

            _reversing = want;
            _reverseTravelled = 0f;
            _steer = 0f;       // 직전까지 꾫고 있던 각을 끌고 가지 않는다.

            return false;
        }

        /// <summary>뒤쪽이 비어 있는지. 확인하지 않으면 후진 중 NavMesh 밖으로 밀려난다.</summary>
        private bool IsReverseBlocked()
        {
            if (_agent == null || !_agent.isOnNavMesh)
            {
                return true;
            }

            // 멈춰 선 차는 자기 NavMeshObstacle이 발밑에 구멍을 낸다. 내 위치에서 쏘면 그 구멍에 바로 막혀
            // 뒤가 늘 막힌 것으로 나오고, 전진도 후진도 못 한 채 기어만 바꾸며 선다. 뒤 범퍼 너머에서부터 본다.
            Vector3 back = -transform.forward;
            // 차는 바퀴가 바닥에 닿도록 피벗이 떠 있다. NavMesh는 바닥 높이에서 찾는다.
            Vector3 origin = transform.position - Vector3.up * _rideHeight;
            float rear = RearBumperDistance();

            Vector3 start = origin + back * (rear + OwnCarveAllowance);
            if (!CarNavMesh.SamplePosition(start, out NavMeshHit startHit, 1f))
            {
                return true;
            }

            Vector3 end = origin + back * (rear + _reverseClearance);
            return CarNavMesh.Raycast(startHit.position, end, out NavMeshHit _);
        }

        /// <summary>내 NavMeshObstacle이 낸 구멍이 차체 밖으로 이만큼(m)까지 번진다고 본다. NavMesh 베이크 반경보다 조금 크게.</summary>
        private const float OwnCarveAllowance = 1f;

        /// <summary>피벗에서 뒤 범퍼까지의 거리(m). 차체 크기는 손님이 피해 가는 NavMeshObstacle 박스에서 읽는다.</summary>
        private float RearBumperDistance()
        {
            if (_footprint == null)
            {
                return _wheelBase;
            }

            Transform t = _footprint.transform;
            Vector3 center = t.TransformPoint(_footprint.center);
            float halfLength = _footprint.shape == NavMeshObstacleShape.Box
                ? _footprint.size.z * 0.5f * Mathf.Abs(t.lossyScale.z)
                : _footprint.radius * Mathf.Abs(t.lossyScale.x);

            // 차체 중심이 피벗보다 뒤에 있으면 뒤 범퍼는 그만큼 더 멀다.
            float centerBehind = -Vector3.Dot(center - transform.position, transform.forward);
            return Mathf.Max(0f, halfLength + centerBehind);
        }

        /// <summary>속도에 연동된 Lookahead. 마지막 직선 구간에서는 더 짧게 잡아 자리에 정확히 들어간다.</summary>
        private float CurrentLookahead()
        {
            float lookahead = Mathf.Clamp(_lookaheadGain * _speed, _lookaheadMin, _lookaheadMax);

            // 마지막 직선 구간은 코너를 자를 일이 없고 자리에 정확히 들어가는 게 전부다.
            // Ld가 길면 차가 선으로 다 모이기 전에 끝점에 닿아, 좌우로 밀린 채 서버린다.
            if (_path.OnFinalLeg)
            {
                lookahead = Mathf.Min(lookahead, _finalLegLookahead);
            }

            return lookahead;
        }

        /// <summary>경로 위에서 목표점을 골라 차 기준 좌표로 돌려준다. 차가 따라갈 수 없는 각이면 Ld를 늘려 다시 고른다.</summary>
        private Vector3 ResolveGoal(Vector3 cursor)
        {
            float lookahead = CurrentLookahead();

            // 앞차를 추월하는 중이면 경로는 그대로 두고 겨누는 점만 옆으로 민다.
            Vector3 avoidance = _traffic != null ? _traffic.AvoidanceOffset : Vector3.zero;

            Vector3 goal = _path.FindGoalPoint(cursor, lookahead) + avoidance;
            Vector3 local = CarSteeringSolver.ToLocalPlanar(transform, goal);

            float required = CarSteeringSolver.RequiredLookahead(local, MinTurnRadius);
            if (lookahead < required)
            {
                goal = _path.FindGoalPoint(cursor, required) + avoidance;
                local = CarSteeringSolver.ToLocalPlanar(transform, goal);
            }

            // Pure Pursuit는 목표점까지 원호를 그리며 가므로, 그 현(차 → 목표점)이 앞으로 쓸고 지나갈 방향을 가장 잘 대표한다.
            _travelDir = goal - transform.position;
            _travelDir.y = 0f;

            return local;
        }

        /// <summary>목표 곱률을 조향각으로 바꿔 변화율까지 제한하고, 실제 적용된 곱률을 돌려준다.</summary>
        private float UpdateSteering(Vector3 local, float dt)
        {
            float maxCurvature = CarSteeringSolver.MaxCurvature(_wheelBase, _maxSteerAngle);

            // 후진 중에는 가고 싶은 방향의 반대로 꾫는다.
            // 속도가 음수라 yawRate = v·κ 의 부호가 뒤집히므로, 이래야 차머리가 목표 쪽으로 돌아온다.
            float curvatureTarget = _reversing
                ? -CarSteeringSolver.DesiredCurvature(local, maxCurvature)
                : CarSteeringSolver.TargetCurvature(local, maxCurvature, MinTurnRadius);

            float steerTarget = CarSteeringSolver.CurvatureToSteer(curvatureTarget, _wheelBase);
            _steer = Mathf.MoveTowards(_steer, steerTarget, _steerRate * Mathf.Deg2Rad * dt);

            return CarSteeringSolver.SteerToCurvature(_steer, _wheelBase);
        }

        /// <summary>최고 속도·코너 감속·제동거리 중 가장 작은 값을 목표로 잡고 가감속한다. 후진은 목표 속도가 음수다.</summary>
        private void UpdateSpeed(float curvature, float dt, bool shifting)
        {
            float vTarget;

            if (shifting)
            {
                // 기어를 바꾸려면 먼저 서야 한다.
                vTarget = 0f;
            }
            else if (_reversing)
            {
                // 허용된 후진 거리 안에 멈춰야 하므로 남은 거리로 상한을 걸어둔다.
                float room = Mathf.Max(_maxReverseDistance - _reverseTravelled, 0f);
                vTarget = -Mathf.Min(_reverseSpeed, CarSteeringSolver.StopSpeedLimit(room, _brakeAccel));
            }
            else
            {
                float vCurve = CarSteeringSolver.CurveSpeedLimit(curvature, _maxLateralAccel);
                float vStop = CarSteeringSolver.StopSpeedLimit(_remaining - ArriveDistance, _comfortBrakeAccel);

                vTarget = Mathf.Min(_agent.speed, Mathf.Min(vCurve, vStop));

                // 자리로 들어가는 구간은 천천히. 보기에도 자연스럽고,
                // 짧은 Lookahead와 짝지어야 흔들리지 않고 선에 밀착해 들어간다.
                if (_path.OnFinalLeg)
                {
                    vTarget = Mathf.Min(vTarget, _finalLegSpeed);
                }
                else if (_path.HasPendingFinalLeg)
                {
                    // 진입점에 닿는 순간 상한이 뚝 떨어지면 급감속이 된다. 거기서 _finalLegSpeed가 되도록 미리 줄여 둔다.
                    float toVia = CarPathTracker.HorizontalDistance(transform.position, _path.ViaPoint);
                    float vVia = Mathf.Sqrt(_finalLegSpeed * _finalLegSpeed + 2f * _comfortBrakeAccel * toVia);
                    vTarget = Mathf.Min(vTarget, vVia);
                }
            }

            // 경로가 갱신돼 남은 거리가 갑자기 줄었으면 편한 감속도로는 목적지를 지나친다. 그때만 세게 밟는다.
            float stopRoom = _remaining - ArriveDistance;
            _hardBrake = shifting || (!_reversing && stopRoom < _speed * _speed / (2f * _comfortBrakeAccel));

            if (!shifting)
            {
                vTarget = LimitByTraffic(vTarget, dt);
            }

            // 줄일 때는 보통 편한 감속도로. 기어를 바꾸거나 앞차 앞에서 그걸로 못 설 때만 세게 밟는다.
            float rate;
            if (Mathf.Abs(vTarget) > Mathf.Abs(_speed))
            {
                rate = _accel;
            }
            else
            {
                rate = _hardBrake || _reversing ? _brakeAccel : _comfortBrakeAccel;
            }

            _speed = Mathf.MoveTowards(_speed, vTarget, rate * dt);
        }

        /// <summary>가는 방향에 다른 차가 있으면 그 앞 _minGap에서 설 수 있는 속도로 묶는다. 경로는 그대로 두고 기다리기만 한다.
        /// 오래 막혀 있으면 물러서기(<see cref="_backoffRemaining"/>)를 건다.</summary>
        private float LimitByTraffic(float vTarget, float dt)
        {
            if (_traffic == null)
            {
                return vTarget;
            }

            // 빠를수록 멀리 봐야 제때 선다.
            // 앞차 때문에 줄일 때는 편한 감속도로 미리 줄인다. 최대 제동으로 늦게 서면 급정거처럼 보인다.
            float stopDistance = _speed * _speed / (2f * _comfortBrakeAccel);
            float range = Mathf.Max(_sensorRange, stopDistance + _minGap);

            // 목적지 너머에 선 차는 상관없다. 앞 범퍼에서 그 차까지 거리가 목적지까지 남은 거리보다 멀면,
            // 목적지에 섰을 때도 그 차 앞에 멈춰 있게 된다. 차간 거리를 더해 넓히면 자리가 촘촘한 줄에서
            // 다음 자리 차를 보고 비켜 서다 자기 자리를 놓친다.
            if (!_reversing)
            {
                range = Mathf.Min(range, _remaining);
            }

            // 앞에 차가 있으면 주차 자리로 들어가는 마지막 구간에서도 비켜 간다. 비켜 선 채 끝점을 지나면
            // 도착 판정(_arrivalRadius)에 걸리지 않아 RetryApproach가 다시 들어오게 한다. 후진은 비키는 동작이 아니라 뺀다.
            bool allowBypass = !_reversing;
            float gap = _traffic.ClearDistance(_reversing, range, allowBypass, _travelDir);

            UpdateBackoff(gap, dt);

            if (float.IsPositiveInfinity(gap))
            {
                return vTarget;
            }

            // 편한 감속도로는 _minGap 앞에서 못 선다. 이때만 세게 밟는다.
            float room = gap - _minGap;
            if (room < _speed * _speed / (2f * _comfortBrakeAccel))
            {
                _hardBrake = true;
            }

            float limit = CarSteeringSolver.StopSpeedLimit(room, _comfortBrakeAccel);
            return Mathf.Sign(vTarget) * Mathf.Min(Mathf.Abs(vTarget), limit);
        }

        /// <summary>앞차에 막혀 서 있는 시간을 재고, 교착이거나 너무 오래 막혔으면 물러서기를 건다.
        /// 서로 마주 보고 선 두 차는 추월할 옆 공간도 없어 누군가 물러서지 않으면 영영 풀리지 않는다.</summary>
        private void UpdateBackoff(float gap, float dt)
        {
            bool stuck = !_reversing && _backoffRemaining <= 0f &&
                         gap <= _minGap + 0.2f && Mathf.Abs(_speed) < 0.05f;

            if (!stuck)
            {
                _blockedTime = 0f;
                return;
            }

            _blockedTime += dt;

            ICarTrafficSensor blocker = _traffic.Blocker;
            bool mutual = blocker != null && ReferenceEquals(blocker.Blocker, _traffic);

            // 교착이면 우선순위가 낮은 쪽만 물러선다. 둘 다 물러서면 다시 마주친다.
            bool yieldMutual = mutual && _traffic.Priority < blocker.Priority && _blockedTime >= _backoffDelay;
            bool tooLong = _blockedTime >= _backoffStuckTime;

            if (!yieldMutual && !tooLong)
            {
                return;
            }

            _blockedTime = 0f;
            _backoffRemaining = _backoffDistance;
            _backoffTimer = 0f;
        }

        /// <summary>회전은 속도에 비례해서만(θ̇ = v·κ), 이동은 항상 정면으로만.</summary>
        private void Integrate(float curvature, float groundY, float dt)
        {
            float yawRate = _speed * curvature;
            transform.Rotate(0f, yawRate * Mathf.Rad2Deg * dt, 0f, Space.World);

            Vector3 next = transform.position + transform.forward * (_speed * dt);
            next.y = GroundHeight(next, groundY) + _rideHeight;
            transform.position = next;

            // 에이전트에 되먹여야 경로 재계산과 다른 에이전트의 회피가 계속 정상 동작한다.
            _agent.nextPosition = transform.position;
        }

        /// <summary>실제 바닥(Ground 레이어 콜라이더) 높이. NavMesh 면은 복셀로 근사해 도로보다 수십 cm 높거나 낮을 수 있어
        /// 그대로 쓰면 차가 떠 있거나 파묻힌다. 바닥을 못 찾으면 경로 높이(fallback)를 쓴다.</summary>
        private float GroundHeight(Vector3 position, float fallback)
        {
            Vector3 origin = position + Vector3.up * GroundProbeUp;
            return Physics.Raycast(origin, Vector3.down, out RaycastHit hit, GroundProbeUp + GroundProbeDown,
                                   _groundMask, QueryTriggerInteraction.Ignore)
                ? hit.point.y
                : fallback;
        }

        /// <summary>지금 자리의 바닥 높이에 맞춰 차를 올리거나 내린다. 스폰하거나 서 있다 출발할 때 한 번 맞춘다.</summary>
        private void SnapToGround()
        {
            Vector3 position = transform.position;
            position.y = GroundHeight(position, position.y - _rideHeight) + _rideHeight;
            transform.position = position;
        }

        /// <summary>피벗에서 차 모델의 가장 낮은 곳(대개 바퀴 바닥)까지의 높이. 모델이 피벗보다 아래로 내려와 있으면
        /// 피벗을 바닥에 맞춘 차는 그만큼 파묻힌다. 손님이 타기 전(Awake)에 재므로 차 자신의 렌더러만 잡힌다.</summary>
        private float MeasureRideHeight()
        {
            if (_rideHeightOverride >= 0f)
            {
                return _rideHeightOverride;
            }

            float lowest = float.PositiveInfinity;
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            {
                lowest = Mathf.Min(lowest, r.bounds.min.y);
            }

            return float.IsPositiveInfinity(lowest) ? 0f : Mathf.Max(0f, transform.position.y - lowest);
        }

        /// <summary>더 따라갈 경로가 없을 때 핸들을 중앙으로 되돌리면서 제동해 세운다.</summary>
        private void Settle(float dt)
        {
            _steer = Mathf.MoveTowards(_steer, 0f, _steerRate * Mathf.Deg2Rad * dt);
            _speed = Mathf.MoveTowards(_speed, 0f, _brakeAccel * dt);

            // 후진 중이면 속도가 음수다. 부호를 뺄고 비교해야 그 자리에서 멈춰버리지 않는다.
            if (Mathf.Abs(_speed) <= 0.001f)
            {
                _speed = 0f;
                _reversing = false;
                _reverseTravelled = 0f;
                return;
            }

            transform.position += transform.forward * (_speed * dt);
            SnapToGround();

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.nextPosition = transform.position;
            }
        }
    }
}
