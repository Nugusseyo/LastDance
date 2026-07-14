using UnityEngine;

namespace JJH._02_Scripts.Agents
{
    public interface ISensor
    {
        public bool IsTargetInSight(Transform targetTrm, float viewAngle);   //시야각 안에 있는가(타겟)
        public bool IsTargetInSight(float viewAngle, float checkDistance, out Collider collid);   //시야각 안에 있는가(타겟X)
        bool IsTargetInViewRadius(float range, out Collider hitCollider); //사거리 안에 있는가(원형 감지)
        int FindTargetsInRadius(float viewRadius); //사거리 안에 얼마나 있는가(원형 감지)
    }
}