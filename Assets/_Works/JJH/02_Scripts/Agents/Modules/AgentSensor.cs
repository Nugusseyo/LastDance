using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public class AgentSensor : AbstractModule, ISensor
    {
        public bool FindWeapon(Transform cameraTrm, LayerMask weaponLayer,
            float distance, out Collider weaponCollider)
        {
            weaponCollider = null;

            if (cameraTrm == null)
                return false;

            if (!Physics.Raycast(cameraTrm.position, cameraTrm.forward,
                out RaycastHit hit, distance, weaponLayer))
                return false;

            weaponCollider = hit.collider;
            return true;
        }
    }
}