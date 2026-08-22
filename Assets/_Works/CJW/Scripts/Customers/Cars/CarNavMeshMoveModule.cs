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
        }
    }
}
