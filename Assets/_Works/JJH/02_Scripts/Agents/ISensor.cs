using UnityEngine;

namespace JJH._02_Scripts.Agents
{
    public interface ISensor
    {
        public bool IsTargetInViewAngle(Transform targetTrm, float viewAngle); //시야각 안에 있는가
        bool IsTargetInSight(Transform targetTrm); //시야각 안에 있는가(벽 감지)
        bool IsTargetInViewRadius(float range, out Collider hitCollider); //사거리 안에 있는가(원형 감지)
        int FindTargetsInRadius(float viewRadius); //사거리 안에 얼마나 있는가(원형 감지)
    }
}