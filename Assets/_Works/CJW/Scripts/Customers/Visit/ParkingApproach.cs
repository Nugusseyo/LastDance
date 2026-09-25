using _Works.CJW.Scripts.Cars;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>주차 자리로 들어가는 마지막 직선(진입점 → 자리)의 계산. 자리를 고르는 VisitDirector와
    /// 진입 방향을 고르는 ArrivingState가 같은 기준으로 판단하도록 한 곳에 둔다.</summary>
    public static class ParkingApproach
    {
        /// <summary>자리에서 진입점까지의 기본 거리(m).</summary>
        public const float DefaultDistance = 9f;

        /// <summary>진입점을 NavMesh 위에서 찾을 때의 기본 허용 오차(m).</summary>
        public const float DefaultSampleRadius = 2f;

        /// <summary>진입 직선이 막혔는지 볼 때의 기본 반경(m). 차 반폭에 여유를 더한 값이다.</summary>
        public const float DefaultClearRadius = 1.2f;

        /// <summary>후면 주차(자리 뒤쪽에서 들어오기)를 기본으로 허용할지. 끄면 일방통행이 된다.</summary>
        public const bool DefaultAllowBackIn = false;

        /// <summary>자리를 정면으로 들어가는 방향과 뒤로 들어가는 방향.</summary>
        public static Quaternion ForwardIn(Quaternion slotRotation) => slotRotation;
        public static Quaternion BackIn(Quaternion slotRotation) => slotRotation * Quaternion.Euler(0f, 180f, 0f);

        /// <summary>자리에서 rotation 정면으로 distance만큼 물러난 진입점을 NavMesh 위에서 찾는다.</summary>
        public static bool TryGetPoint(Vector3 arrival, Quaternion rotation, float distance, float sampleRadius, out Vector3 point)
        {
            Vector3 candidate = arrival - rotation * Vector3.forward * distance;

            if (CarNavMesh.SamplePosition(candidate, out NavMeshHit hit, sampleRadius))
            {
                point = hit.position;
                return true;
            }

            point = candidate;
            return false;
        }

        /// <summary>진입점에서 자리까지의 직선 위에 다른 차가 없는지. 마지막 구간은 NavMesh를 보지 않는 직선이라
        /// 그 위에 차가 서 있으면 비켜 가지도 못하고 그 뒤에서 멈춘다.</summary>
        public static bool IsLegClear(Vector3 approach, Vector3 arrival, float radius, ICarTrafficSensor ignore = null)
            => CarTraffic.IsSegmentClear(approach, arrival, radius, ignore);
    }
}
