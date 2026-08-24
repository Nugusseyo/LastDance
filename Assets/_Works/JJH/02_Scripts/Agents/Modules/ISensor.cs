using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public interface ISensor
    {
        Collider[] ColliderResults { get; }

        bool IsTargetInSight(Transform targetTrm, float viewAngle);   //시야각 안에 있는가(타겟)
        bool IsTargetInSight(float viewAngle, float checkDistance);   //시야각 안에 있는가(타겟X)
        bool IsTargetInViewRadius(float range, out Collider hitCollider); //사거리 안에 있는가(원형 감지)
        int FindTargetsInRadius(float viewRadius); //사거리 안에 얼마나 있는가(원형 감지)
    }
}