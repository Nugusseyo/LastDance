using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public class AgentSensor : AbstractModule, ISensor
    {
        [SerializeField] private LayerMask groundLayer;
        [SerializeField] private float detectRadius = 1.5f;

        private Transform _cameraTrm;
        private float _distance;

        private Vector3 _hitPoint;
        private bool _hasHit;

        public bool FindItem(Transform cameraTrm, LayerMask weaponLayer,
                                            float distance, out Collider weaponCollider)
        {
            weaponCollider = null;
            _hasHit = false;

            if (cameraTrm == null)
                return false;

            _cameraTrm = cameraTrm;
            _distance = distance;

            if (Physics.Raycast(cameraTrm.position, cameraTrm.forward,
                    out RaycastHit itemHit, distance, weaponLayer))
            {
                _hitPoint = itemHit.point;
                _hasHit = true;
                weaponCollider = itemHit.collider;
                return true;
            }

            if (!Physics.Raycast(cameraTrm.position, cameraTrm.forward,
                 out RaycastHit groundHit, distance, groundLayer))
                return false;

            _hitPoint = groundHit.point;
            _hasHit = true;

            Collider[] items = Physics.OverlapSphere(_hitPoint, detectRadius, weaponLayer);
            if (items.Length == 0)
                return false;

            float bestDist = float.MaxValue;
            Collider best = null;

            foreach (var col in items)
            {
                Vector3 closestPointOnCollider = col.ClosestPoint(cameraTrm.position);
                float distToRay = DistancePointToSegment(
                                                closestPointOnCollider, cameraTrm.position,
                                                cameraTrm.position + cameraTrm.forward * distance);

                if (distToRay < bestDist)
                {
                    bestDist = distToRay;
                    best = col;
                }
            }

            weaponCollider = best;
            return best != null;
        }

        private static float DistancePointToSegment(Vector3 point, Vector3 segStart, Vector3 segEnd)
        {
            Vector3 segDir = segEnd - segStart;
            float segLenSq = segDir.sqrMagnitude;

            if (segLenSq < Mathf.Epsilon)
                return Vector3.Distance(point, segStart);

            float t = Mathf.Clamp01(Vector3.Dot(point - segStart, segDir) / segLenSq);
            Vector3 projection = segStart + t * segDir;

            return Vector3.Distance(point, projection);
        }

        private void OnDrawGizmos()
        {
            if (_cameraTrm == null)
                return;

            Gizmos.color = Color.red;
            Gizmos.DrawRay(_cameraTrm.position, _cameraTrm.forward * _distance);

            if (_hasHit)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_hitPoint, detectRadius);
            }
        }
    }
}