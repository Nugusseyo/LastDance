using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Cars
{
    /// <summary>
    /// NavMeshAgent를 경로 계산기로만 쓰고, 이동은 Pure Pursuit + 자전거 모델로 직접 굴리는 차량 이동 모듈.
    /// 에이전트에 이동을 맡기면 경로의 꺾은선을 그대로 따라가며 제자리 회전까지 해버려서 차처럼 보이지 않는다.
    ///
    /// 매 프레임 하는 일은 네 가지다.
    /// 1. 경로 위에서 Lookahead 거리만큼 앞의 목표점을 고른다. (Pure Pursuit)
    /// 2. 지금 위치·방향에서 그 점에 닿는 원호의 곡률을 구하고, 조향각으로 바꿔 한계 안에 가둔다. (자전거 모델)
    /// 3. 곡률과 남은 거리로 목표 속도를 정해 가감속한다.
    /// 4. 회전은 속도에 비례해서만 시키고, 이동은 항상 정면으로만 한다.
    ///
    /// 4번이 이 모듈의 전부다. 멈춘 차는 돌지 않고, 차는 옆으로 미끄러지지 않는다.
    /// </summary>
    public class CarSteeringMoveModule : AbstractModule, ICarMoveModule, IUpdate
    {
        private const int MaxCorners = 256;

        [Header("참조")]
        [SerializeField] private NavMeshAgent _agent;

        [Header("도착 판정")]
        [Tooltip("stoppingDistance가 이보다 작으면 이 값을 도착 판정에 쓴다.")]
        [SerializeField] private float _arriveThreshold = 0.5f;

        [Tooltip("회피 우선순위. 낮을수록 먼저다. 손님(기본 50)보다 낮게 두어야 차가 손님을 피하지 않는다.")]
        [SerializeField] private int _avoidancePriority;

        [Header("차체 (자전거 모델)")]
        [Tooltip("앞축과 뒷축 사이 거리(m). 회전 반경의 기준이다. 이 오브젝트의 피벗은 뒷축에 있는 편이 자연스럽다.")]
        [SerializeField, Min(0.1f)] private float _wheelBase = 2.5f;

        [Tooltip("최대 조향각(도). 최소 회전 반경 = 축거 / tan(이 각). 작을수록 크게 도는 큰 차가 된다.")]
        [SerializeField, Range(5f, 70f)] private float _maxSteerAngle = 35f;

        [Tooltip("핸들이 꺾이는 속도(도/초). 낮을수록 코너 진입이 완만해진다.")]
        [SerializeField, Min(1f)] private float _steerRate = 180f;

        [Header("속도")]
        [Tooltip("가속도(m/s²). 최고 속도는 NavMeshAgent.Speed를 쓴다.")]
        [SerializeField, Min(0.1f)] private float _accel = 6f;

        [Tooltip("감속도(m/s²). 가속보다 크게 두는 편이 자연스럽다.")]
        [SerializeField, Min(0.1f)] private float _brakeAccel = 10f;

        [Tooltip("허용 횡가속도(m/s²). 코너에서 얼마나 속도를 줄일지 정한다. 4~6이 얌전한 승용차.")]
        [SerializeField, Min(0.5f)] private float _maxLateralAccel = 5f;

        [Header("Pure Pursuit")]
        [Tooltip("Lookahead = 속도 × 이 값. 클수록 멀리 보고 부드럽게 가지만 코너를 크게 자른다.")]
        [SerializeField, Min(0f)] private float _lookaheadGain = 0.8f;

        [Tooltip("Lookahead 하한(m). 차 길이 정도가 무난하다. 너무 작으면 좌우로 떨린다.")]
        [SerializeField, Min(0.1f)] private float _lookaheadMin = 3f;

        [Tooltip("Lookahead 상한(m).")]
        [SerializeField, Min(0.1f)] private float _lookaheadMax = 8f;

        [Header("경로")]
        [Tooltip("경로를 다시 읽는 주기(초). NavMesh가 경로를 갈아끼워도 이 주기 안에 따라잡는다.")]
        [SerializeField, Min(0.05f)] private float _pathRefreshInterval = 0.4f;

        private readonly Vector3[] _corners = new Vector3[MaxCorners];
        private int _cornerCount;
        private int _segIndex;

        private bool _hasDestination;
        private float _refreshTimer;

        private float _speed;

        /// <summary>현재 조향각(라디안). 곡률이 아니라 각도로 들고 있어야 한계와 변화율을 물리적으로 자를 수 있다.</summary>
        private float _steer;

        private float _remaining = float.PositiveInfinity;

        /// <summary>현재 속도(m/s). 바퀴 회전이나 엔진음에 쓰면 된다.</summary>
        public float Speed => _speed;

        /// <summary>현재 조향각(도). 앞바퀴를 실제로 돌려 보여줄 때 쓴다.</summary>
        public float SteerAngleDeg => _steer * Mathf.Rad2Deg;

        /// <summary>최소 회전 반경(m). 자전거 모델에서 나오는 이 차의 물리적 한계다.</summary>
        public float MinTurnRadius => _wheelBase / Mathf.Tan(_maxSteerAngle * Mathf.Deg2Rad);

        private float MaxCurvature => Mathf.Tan(_maxSteerAngle * Mathf.Deg2Rad) / _wheelBase;

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

                return _remaining <= ArriveDistance;
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
            // 경로를 여기서부터 뽑게 먼저 맞춰준다.
            _agent.Warp(transform.position);

            _agent.isStopped = false;
            _hasDestination = _agent.SetDestination(destination);

            if (!_hasDestination)
            {
                Debug.LogWarning($"[CarSteering] {name}의 목적지 {destination}까지 경로를 찾지 못했습니다.", this);
                return;
            }

            _cornerCount = 0;
            _segIndex = 0;
            _refreshTimer = 0f;
            _remaining = float.PositiveInfinity;
        }

        public void Stop()
        {
            _hasDestination = false;
            _cornerCount = 0;
            _segIndex = 0;
            _speed = 0f;
            _steer = 0f;
            _remaining = float.PositiveInfinity;

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

        public void OnUpdate(float dt)
        {
            if (!_hasDestination || _agent == null || dt <= 0f)
            {
                return;
            }

            RefreshCornersIfNeeded(dt);

            if (_cornerCount < 2)
            {
                return;
            }

            // 1. 경로 위 현재 지점과 남은 거리
            Vector3 position = transform.position;
            Vector3 cursor = AdvanceAlongPath(position, out _remaining);

            if (_remaining <= ArriveDistance && _speed <= 0.01f)
            {
                _speed = 0f;
                return;
            }

            // 2. Pure Pursuit — Lookahead는 속도에 연동한다. 빠를수록 멀리 본다.
            float lookahead = Mathf.Clamp(_lookaheadGain * _speed, _lookaheadMin, _lookaheadMax);
            Vector3 goal = FindGoalPoint(cursor, lookahead);

            Vector3 local = transform.InverseTransformPoint(goal);
            local.y = 0f;

            float maxCurv = MaxCurvature;
            float curvTarget;

            if (local.z <= 0.01f)
            {
                // 목표점이 옆이나 뒤에 있으면 원호 공식이 무너진다. 최대 조향으로 크게 돌아 나간다.
                curvTarget = local.x >= 0f ? maxCurv : -maxCurv;
            }
            else
            {
                // κ = 2·x / Ld²  — 지금 방향에 접하면서 목표점을 지나는 원의 곡률
                curvTarget = 2f * local.x / Mathf.Max(local.sqrMagnitude, 1e-4f);
                curvTarget = Mathf.Clamp(curvTarget, -maxCurv, maxCurv);
            }

            // 3. 자전거 모델 — 곡률을 조향각으로 바꿔서 변화율까지 제한한다.
            //    이걸 해야 코너 진입에서 곡률이 계단처럼 튀지 않고 완화곡선을 그린다.
            float steerTarget = Mathf.Atan(_wheelBase * curvTarget);
            _steer = Mathf.MoveTowards(_steer, steerTarget, _steerRate * Mathf.Deg2Rad * dt);

            float curv = Mathf.Tan(_steer) / _wheelBase;

            // 4. 속도 — 코너에서는 횡가속도 한계까지, 목적지 앞에서는 제동거리에 맞춰 줄인다.
            float vCurve = Mathf.Sqrt(_maxLateralAccel / Mathf.Max(Mathf.Abs(curv), 1e-4f));
            float vStop = Mathf.Sqrt(2f * _brakeAccel * Mathf.Max(_remaining - ArriveDistance, 0f));
            float vTarget = Mathf.Min(_agent.speed, Mathf.Min(vCurve, vStop));

            _speed = Mathf.MoveTowards(_speed, vTarget, (vTarget > _speed ? _accel : _brakeAccel) * dt);

            // 5. 적분 — 회전은 속도에 비례해서만(θ̇ = v·κ), 이동은 항상 정면으로만.
            float yawRate = _speed * curv;
            transform.Rotate(0f, yawRate * Mathf.Rad2Deg * dt, 0f, Space.World);

            Vector3 next = transform.position + transform.forward * (_speed * dt);
            next.y = cursor.y;
            transform.position = next;

            // 에이전트에 되먹여야 경로 재계산과 다른 에이전트의 회피가 계속 정상 동작한다.
            _agent.nextPosition = transform.position;
        }

        private void RefreshCornersIfNeeded(float dt)
        {
            _refreshTimer -= dt;

            if (_cornerCount >= 2 && _refreshTimer > 0f)
            {
                return;
            }

            if (_agent.pathPending)
            {
                return;
            }

            _refreshTimer = _pathRefreshInterval;

            NavMeshPath path = _agent.path;
            if (path == null)
            {
                _cornerCount = 0;
                return;
            }

            _cornerCount = path.GetCornersNonAlloc(_corners);
            _segIndex = 0;
        }

        /// <summary>
        /// 차를 경로에 투영해 현재 지점을 구하고, 지나온 세그먼트를 버린다.
        /// 인덱스를 앞으로만 밀기 때문에 경로가 자기 근처로 되돌아와도 뒷구간에 붙지 않는다.
        /// </summary>
        private Vector3 AdvanceAlongPath(Vector3 position, out float remaining)
        {
            while (_segIndex < _cornerCount - 2)
            {
                if (ProjectOnSegment(_corners[_segIndex], _corners[_segIndex + 1], position) < 0.999f)
                {
                    break;
                }

                _segIndex++;
            }

            float t = ProjectOnSegment(_corners[_segIndex], _corners[_segIndex + 1], position);
            Vector3 cursor = Vector3.Lerp(_corners[_segIndex], _corners[_segIndex + 1], t);

            remaining = HorizontalDistance(cursor, _corners[_segIndex + 1]);
            for (int i = _segIndex + 1; i < _cornerCount - 1; i++)
            {
                remaining += HorizontalDistance(_corners[i], _corners[i + 1]);
            }

            return cursor;
        }

        /// <summary>경로를 따라 lookahead만큼 앞선 점. 경로 끝을 넘으면 마지막 코너를 준다.</summary>
        private Vector3 FindGoalPoint(Vector3 cursor, float lookahead)
        {
            float left = lookahead;
            Vector3 from = cursor;
            Vector3 goal = _corners[_cornerCount - 1];

            for (int i = _segIndex + 1; i < _cornerCount; i++)
            {
                Vector3 to = _corners[i];
                float d = HorizontalDistance(from, to);

                if (d >= left)
                {
                    return Vector3.Lerp(from, to, left / Mathf.Max(d, 1e-4f));
                }

                left -= d;
                from = to;
                goal = to;
            }

            return goal;
        }

        private static float ProjectOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            ab.y = 0f;

            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 1e-6f)
            {
                return 1f;
            }

            Vector3 ap = p - a;
            ap.y = 0f;

            return Mathf.Clamp01(Vector3.Dot(ap, ab) / lengthSqr);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_lookaheadMax < _lookaheadMin)
            {
                _lookaheadMax = _lookaheadMin;
            }
        }

        private void OnDrawGizmosSelected()
        {
            // 최소 회전 반경
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + transform.right * MinTurnRadius, 0.15f);
            Gizmos.DrawWireSphere(transform.position - transform.right * MinTurnRadius, 0.15f);

            if (!Application.isPlaying || _cornerCount < 2)
            {
                return;
            }

            // 남은 경로
            Gizmos.color = Color.yellow;
            for (int i = _segIndex; i < _cornerCount - 1; i++)
            {
                Gizmos.DrawLine(_corners[i], _corners[i + 1]);
            }

            // 목표점
            Vector3 cursor = AdvanceAlongPath(transform.position, out _);
            float lookahead = Mathf.Clamp(_lookaheadGain * _speed, _lookaheadMin, _lookaheadMax);

            // Pure Pursuit이 그리는 원의 반경은 Ld / (2·sin α)라, 목표점이 옆으로 붙을수록 작아진다.
            // 그 원이 최소 회전 반경보다 작아지면 차는 따라갈 수 없어 최대 조향에 물린 채
            // 목표점 주위를 빙빙 돌게 된다. Ld를 회전원의 지름 아래로는 내리지 않아 아예 막는다.
            lookahead = Mathf.Max(lookahead, 2f * MinTurnRadius);
            Vector3 goal = FindGoalPoint(cursor, lookahead);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, goal);
            Gizmos.DrawWireSphere(goal, 0.3f);
        }
#endif
    }
}
