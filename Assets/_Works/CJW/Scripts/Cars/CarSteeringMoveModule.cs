using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.ManagingAgents;
using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>
    /// NavMeshAgent를 경로 계산기로만 쓰고, 이동은 Pure Pursuit + 자전거 모델로 직접 굴리는 차량 이동 모듈.
    /// 에이전트에 이동을 맡기면 경로의 꾫은선을 그대로 따라가며 제자리 회전까지 해버려서 차처럼 보이지 않는다.
    ///
    /// 매 프레임 하는 일은 네 가지다.
    /// 1. 경로 위에서 Lookahead 거리만큼 앞의 목표점을 고른다. (Pure Pursuit)
    /// 2. 지금 위치·방향에서 그 점에 닿는 원호의 곱률을 구하고, 조향각으로 바꿔 한계 안에 가둔다. (자전거 모델)
    /// 3. 곱률과 남은 거리로 목표 속도를 정해 가감속한다.
    /// 4. 회전은 속도에 비례해서만 시키고, 이동은 항상 정면으로만 한다.
    ///
    /// 4번이 이 모듈의 전부다. 멈춘 차는 돌지 않고, 차는 옆으로 미끄러지지 않는다.
    ///
    /// 구성
    ///   이 파일            — 인스펙터 값, ICarMoveModule 구현, 매 프레임 순서
    ///   CarPathTracker    — 경로 코너 관리와 목표점 찾기
    ///   CarSteeringSolver — 수식(자전거 모델, Pure Pursuit, 속도 상한)
    ///   ...Gizmos.cs      — 씨 뷰 표시 (에디터 전용)
    /// </summary>
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

        /// <summary>
        /// 목적지까지 실제로 닿는 경로인지. SetDestination은 부분 경로에도 true를 돌려주므로,
        /// 이걸 보지 않으면 차가 닿는 데까지만 가서 거기를 "도착"으로 친다.
        /// </summary>
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

        /// <summary>
        /// approachFrom을 먼저 지나 destination에 닿는다. 둘은 하나의 연속된 경로라
        /// 중간에서 멈추지 않고, 마지막 구간이 직선이라 달리는 동안 방향이 저절로 맞는다.
        /// </summary>
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

            if (!_agent.isOnNavMesh)
            {
                Debug.LogWarning($"[CarSteering] {name}이(가) NavMesh 위에 없어 이동할 수 없습니다.", this);
                _hasDestination = false;
                return;
            }

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
            _retryCount = 0;
            _giveUp = false;
            _remaining = float.PositiveInfinity;
            _endDistance = float.PositiveInfinity;
            _path.Clear();

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

        /// <summary>매 프레임의 순서. 자세한 계산은 아래 네 메서드에 나눠둔다.</summary>
        /// <summary>매 프레임의 순서. 자세한 계산은 아래 메서드들에 나눠둔다.</summary>
        /// <summary>매 프레임의 순서. 자세한 계산은 아래 메서드들에 나눠둔다.</summary>
        public void OnUpdate(float dt)
        {
            if (!_hasDestination || _agent == null || dt <= 0f)
            {
                return;
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

        /// <summary>
        /// 자리에 못 붙은 채 끝점을 지났을 때 처음부터 다시 들어온다.
        ///
        /// 진입점을 쓰는 경로였다면 그 진입점으로 되돌아간다 — 보통 자리 뒤쪽이므로
        /// 차는 한 바퀴 돌거나, 후진으로 각을 벌어 다시 진입한다.
        ///
        /// 횟수를 제한하는 이유는 분명하다. 자리 앞이 막혔거나 진입선이 말도 안 되면
        /// 영원히 재시도하게 되고, 그러면 방문이 끝나지 않아 주차 자리가 반납되지 않는다.
        /// </summary>
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

        /// <summary>
        /// 전진과 후진 중 어느 쪽인지 정한다. 기어를 바꾸는 중이면 true를 돌려준다.
        ///
        /// 후진은 경로를 따라가는 동작이 아니라 <b>각을 벌기 위한 동작</b>이다.
        /// 전진으로 목표에 닿을 수 없을 때만 들어가고, 닿을 수 있게 되면 바로 나온다.
        ///
        /// 기어를 바로 뒤집지 않고 먼저 세우는 이유는 둘이다.
        /// 실제 차가 그렇고, 속도 부호가 한 프레임에 뒤집힐 때 생기는 튀을 없앱니다.
        /// </summary>
        private bool UpdateGear(Vector3 local, float dt)
        {
            bool want;

            if (!_reversing)
            {
                // 핸들을 끝까지 꾫어도 못 닿는다. 물러나야 한다.
                want = !CarSteeringSolver.CanReachForward(local, MinTurnRadius);
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

        /// <summary>
        /// 뒤쪽이 비어 있는지. 후진은 경로를 보고 하는 게 아니라
        /// 확인하지 않으면 NavMesh 밖으로 밀려나가 거기서 걱치게 된다.
        /// </summary>
        private bool IsReverseBlocked()
        {
            if (_agent == null || !_agent.isOnNavMesh)
            {
                return true;
            }

            Vector3 from = transform.position;
            Vector3 to = from - transform.forward * _reverseClearance;

            return NavMesh.Raycast(from, to, out NavMeshHit _, NavMesh.AllAreas);
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

        /// <summary>
        /// 경로 위에서 결눈 점을 고라 차 기준 좌표로 돌려준다.
        /// 한 번 고른 뒤 각도를 보고, 차가 따라갈 수 없는 거리면 Ld를 늘려 다시 고른다.
        /// </summary>
        private Vector3 ResolveGoal(Vector3 cursor)
        {
            float lookahead = CurrentLookahead();

            Vector3 goal = _path.FindGoalPoint(cursor, lookahead);
            Vector3 local = CarSteeringSolver.ToLocalPlanar(transform, goal);

            float required = CarSteeringSolver.RequiredLookahead(local, MinTurnRadius);
            if (lookahead < required)
            {
                goal = _path.FindGoalPoint(cursor, required);
                local = CarSteeringSolver.ToLocalPlanar(transform, goal);
            }

            return local;
        }

        /// <summary>
        /// 목표 곱률을 조향각으로 바꿔 변화율까지 제한하고, 실제 적용된 곱률을 돌려준다.
        /// 변화율 제한이 있어야 코너 진입에서 곱률이 계단처럼 튀지 않고 부드럽게 이어진다.
        /// </summary>
        /// <summary>
        /// 목표 곱률을 조향각으로 바꿔 변화율까지 제한하고, 실제 적용된 곱률을 돌려준다.
        /// 변화율 제한이 있어야 코너 진입에서 곱률이 계단처럼 튀지 않고 부드럽게 이어진다.
        /// </summary>
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

        /// <summary>최고 속도·코너 감속·제동거리 중 가장 작은 값을 목표로 잡고 가감속한다.</summary>
        /// <summary>
        /// 최고 속도·코너 감속·제동거리 중 가장 작은 값을 목표로 잡고 가감속한다.
        /// 후진은 목표 속도가 음수다. 가감속 판단을 크기로 하므로 부호가 섞여도 그대로 동작한다.
        /// </summary>
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
                float vStop = CarSteeringSolver.StopSpeedLimit(_remaining - ArriveDistance, _brakeAccel);

                vTarget = Mathf.Min(_agent.speed, Mathf.Min(vCurve, vStop));

                // 자리로 들어가는 구간은 천천히. 보기에도 자연스럽고,
                // 짧은 Lookahead와 짝지어야 흔들리지 않고 선에 밀착해 들어간다.
                if (_path.OnFinalLeg)
                {
                    vTarget = Mathf.Min(vTarget, _finalLegSpeed);
                }
            }

            float rate = Mathf.Abs(vTarget) > Mathf.Abs(_speed) ? _accel : _brakeAccel;
            _speed = Mathf.MoveTowards(_speed, vTarget, rate * dt);
        }

        /// <summary>
        /// 회전은 속도에 비례해서만(θ̇ = v·κ), 이동은 항상 정면으로만.
        /// 이 두 줄이 이 모듈의 전부다. 멈춘 차가 못 돌고 게걸음이 불가능한 이유도 여기에 있다.
        /// </summary>
        private void Integrate(float curvature, float groundY, float dt)
        {
            float yawRate = _speed * curvature;
            transform.Rotate(0f, yawRate * Mathf.Rad2Deg * dt, 0f, Space.World);

            Vector3 next = transform.position + transform.forward * (_speed * dt);
            next.y = groundY;
            transform.position = next;

            // 에이전트에 되먹여야 경로 재계산과 다른 에이전트의 회피가 계속 정상 동작한다.
            _agent.nextPosition = transform.position;
        }

        /// <summary>
        /// 더 따라갈 경로가 없을 때 핸들을 중앙으로 되돌리면서 제동해 세운다.
        /// 그냥 멈춰버리면 조향각이 남아 다음 출발 첫 프레임에 차가 튀다.
        /// </summary>
        /// <summary>
        /// 더 따라갈 경로가 없을 때 핸들을 중앙으로 되돌리면서 제동해 세운다.
        /// 그냥 멈춰버리면 조향각이 남아 다음 출발 첫 프레임에 차가 튀다.
        /// </summary>
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

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.nextPosition = transform.position;
            }
        }
    }
}
