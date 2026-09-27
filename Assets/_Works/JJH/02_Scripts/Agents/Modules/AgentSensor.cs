using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public class AgentSensor : AbstractModule, ISensor
    {
        private Transform _cameraTrm;
        private float _distance;

        public bool FindItem(Transform cameraTrm, LayerMask weaponLayer,
            float distance, out Collider weaponCollider)
        {
            weaponCollider = null;

            if (cameraTrm == null)
                return false;

            _cameraTrm = cameraTrm;
            _distance = distance;

            if (!Physics.Raycast(cameraTrm.position, cameraTrm.forward,
                out RaycastHit hit, distance, weaponLayer))
                return false;

            weaponCollider = hit.collider;
            return true;
        }

        private void OnDrawGizmos()
        {
            if (_cameraTrm == null)
                return;

            Gizmos.color = Color.red;
            Gizmos.DrawRay(_cameraTrm.position, _cameraTrm.forward * _distance);
        }
    }
}