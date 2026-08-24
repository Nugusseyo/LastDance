using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Cars
{
    /// <summary>
    /// NavMesh로 움직이는 차량 이동 모듈. 스플라인이나 웨이포인트로 바꾸려면
    /// ICarMoveModule을 구현한 다른 모듈로 교체하면 된다.
    /// </summary>
    public class CarNavMeshMoveModule : AbstractModule, ICarMoveModule
    {
        [SerializeField] private NavMeshAgent _agent;
        [Tooltip("stoppingDistance가 이보다 작으면 이 값을 도착 판정에 쓴다.")]
        [SerializeField] private float _arriveThreshold = 0.5f;

        [Tooltip("회피 우선순위. 낮을수록 먼저다. 손님(기본 50)보다 낮게 두어야 차가 손님을 피하지 않는다.")]
        [SerializeField] private int _avoidancePriority;

        private bool _hasDestination;

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

                return _agent.remainingDistance <= Mathf.Max(_arriveThreshold, _agent.stoppingDistance);
            }
        }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            if (_agent == null)
            {
                _agent = GetComponent<NavMeshAgent>();
            }

            if (_agent != null)
            {
                _agent.avoidancePriority = _avoidancePriority;
            }
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
            if (!_agent.isOnNavMesh)
            {
                Debug.LogWarning($"[CarMove] {name}이(가) NavMesh 위에 없어 이동할 수 없습니다.", this);
                _hasDestination = false;
                return;
            }

            // 정차 중에 끊어둔 위치·회전 갱신을 되살린다.
            // 그 사이에 AlignTo가 transform을 직접 돌렸으므로 에이전트 내부 위치를 먼저 맞춰준다.
            if (!_agent.updatePosition)
            {
                _agent.updatePosition = true;
                _agent.updateRotation = true;
                _agent.Warp(transform.position);
            }

            _agent.isStopped = false;
            _hasDestination = _agent.SetDestination(destination);

            if (!_hasDestination)
            {
                Debug.LogWarning($"[CarMove] {name}의 목적지 {destination}까지 경로를 찾지 못했습니다.", this);
            }
        }

        public void Stop()
        {
            _hasDestination = false;

            if (!_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.ResetPath();
            _agent.velocity = Vector3.zero;

            // 정차한 동안은 크라우드 시뮬레이션이 transform을 건드리지 못하게 한다.
            // 손님이 바로 옆에서 내려도 차가 밀리지 않는다.
            // 에이전트 자체는 켜 둔 채라 손님들은 여전히 차를 피해 간다.
            _agent.updatePosition = false;
            _agent.updateRotation = false;
        }
    }
}
