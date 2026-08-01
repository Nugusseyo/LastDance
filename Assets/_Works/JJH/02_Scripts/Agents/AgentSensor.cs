using DevLib.ModuleSystem;
using System;
using System.Linq;
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

        protected float _debugViewRadius = 0;
        protected float _debugViewAngle = 0;
        protected float _debugCheckDistance = 0;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            Debug.Assert(maxColliderCount > 0, $"최대 콜라이더 수는 0보다 커야 합니다.: {gameObject}");
            _colliderResults = new Collider[maxColliderCount];
        }



        //시야각 안에 있는가(타겟)
        public bool IsTargetInSight(Transform targetTrm, float viewAngle)
        {
            ClearColliderResults();

            _debugViewAngle = viewAngle;

            Vector3 direction = targetTrm.position - transform.position;
            direction.y = 0;
            float angle = Vector3.Angle(transform.forward, direction);

            if (angle > viewAngle * 0.5f)
                return false;

            Collider targetCollider = targetTrm.GetComponent<Collider>();
            if (targetCollider != null)
                ColliderResults[0] = targetCollider;

            return true;
        }

        //시야각 안에 있는가(타겟X)
        public bool IsTargetInSight(float viewAngle, float checkDistance)
        {
            ClearColliderResults();

            _debugViewAngle = viewAngle;
            _debugCheckDistance = checkDistance;

            Collider[] targets = Physics.OverlapSphere(transform.position, checkDistance, whatIsTarget);

            int resultIndex = 0;
            foreach (Collider target in targets)
            {
                Vector3 direction = target.transform.position - transform.position;
                float angle = Vector3.Angle(transform.forward, direction);

                if (angle > viewAngle * 0.5f)
                    continue;

                float distance = direction.magnitude;
                if (Physics.Raycast(transform.position, direction.normalized,
                                              distance, whatIsObstacle))
                    continue;

                if (resultIndex >= ColliderResults.Length)
                    break;

                ColliderResults[resultIndex] = target;
                resultIndex++;
            }

            return resultIndex > 0;
        }

        //사거리 안에 있는가(원형 감지)
        public bool IsTargetInViewRadius(float range, out Collider hitCollider)
        {
            _debugViewRadius = range;

            hitCollider = Physics.OverlapSphere(transform.position, range, whatIsTarget)
                                            .FirstOrDefault();
            return hitCollider != null;
        }

        //사거리 안에 얼마나 있는가(원형 감지)
        public int FindTargetsInRadius(float viewRadius)
        {
            _debugViewRadius = viewRadius;

            return Physics.OverlapSphereNonAlloc(transform.position, viewRadius,
                                                                         _colliderResults, whatIsTarget);
        }

        private void ClearColliderResults()
        {
            if (ColliderResults == null)
                return;

            Array.Clear(ColliderResults, 0, ColliderResults.Length);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _debugViewRadius);

            Gizmos.color = Color.blue;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 previousDirection = Quaternion.AngleAxis(-_debugViewAngle * 0.5f, Vector3.up) * forward;
            Gizmos.DrawLine(transform.position, transform.position + previousDirection * _debugCheckDistance);

            Gizmos.color = Color.red;
            for (int i = 1; i <= 30; i++)
            {
                float angle = Mathf.Lerp(-_debugViewAngle * 0.5f, _debugViewAngle * 0.5f, i / 30f);
                Vector3 currentDirection = Quaternion.AngleAxis(angle, Vector3.up) * forward;

                Gizmos.DrawLine(transform.position + previousDirection * _debugCheckDistance, transform.position + currentDirection * _debugCheckDistance);

                previousDirection = currentDirection;
            }

            Gizmos.DrawLine(transform.position, transform.position + previousDirection * _debugCheckDistance);
        }
    }
}