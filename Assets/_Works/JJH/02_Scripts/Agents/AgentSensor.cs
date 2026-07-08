using DevLib.ModuleSystem;
using UnityEngine;

namespace JJH._02_Scripts.Agents
{
    public class AgentSensor : AbstractModule, ISensor
    {
        [SerializeField] private LayerMask whatIsTarget;
        [SerializeField] private LayerMask whatIsObstacle;
        [SerializeField] private int maxColliderCount = 5;

        public Collider[] ColliderResults => _colliderResults;
        private Collider[] _colliderResults;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            Debug.Assert(maxColliderCount > 0, $"최대 콜라이더 수는 0보다 커야 합니다.: {gameObject}");
            _colliderResults = new Collider[maxColliderCount];
        }

        //시야각 안에 있는가
        public bool IsTargetInViewAngle(Transform targetTrm, float viewAngle)
        {
            Vector3 direction = targetTrm.position - transform.position;
            direction.y = 0;
            float angle = Vector3.Angle(transform.forward, direction);
            return angle <= viewAngle * 0.5f;
        }

        //시야각 안에 있는가(벽 감지)
        public bool IsTargetInSight(Transform targetTrm)
        {
            Vector3 targetPosition = targetTrm.position;
            Vector3 direction = targetPosition - transform.position;
            direction.y = 0;
            float distance = direction.magnitude;
            if (Physics.Raycast(transform.position, direction.normalized,
                    out RaycastHit hit, distance, whatIsObstacle))
            {
                Debug.Log($"장애물 감지: {hit.collider.gameObject.name}");
                return false;
            }

            return true;
        }

        //사거리 안에 있는가(원형 감지)
        public bool IsTargetInViewRadius(Transform targetTrm, float viewRadius)
            => (targetTrm.position - transform.position).sqrMagnitude <= viewRadius * viewRadius;
        //사거리 안에 얼마나 있는가(원형 감지)
        public int FindTargetsInRadius(float viewRadius)
            => Physics.OverlapSphereNonAlloc(transform.position, viewRadius, _colliderResults, whatIsTarget);
    }
}