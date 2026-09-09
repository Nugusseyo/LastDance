using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public interface ISensor
    {
        bool FindItem(Transform cameraTrm, LayerMask weaponLayer,
                                float distance, out Collider weaponCollider);
    }
}