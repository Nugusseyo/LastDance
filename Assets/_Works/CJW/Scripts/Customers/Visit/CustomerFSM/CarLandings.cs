using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>차 둘레에서 사람이 설 수 있는 자리 후보. 내릴 때와, 내린 자리가 막혀 차 반대편으로 옮길 때 함께 쓴다.</summary>
    public static class CarLandings
    {
        /// <summary>후보를 차에서 이만큼 띄운다(m). 차가 파낸 구멍 바로 바깥에 선다.</summary>
        private const float Margin = 0.7f;

        /// <summary>후보를 NavMesh에 붙일 때 허용하는 거리(m). 넘기면 차 너머 엉뚱한 쪽으로 붙은 것이라 버린다.</summary>
        private const float Snap = 0.8f;

        /// <summary>후보를 NavMesh에 붙여 <paramref name="into"/>에 모은다. 차가 정한 하차 지점과 그 반대편이 먼저고,
        /// 차가 파낸 구멍(NavMeshObstacle 상자)의 양옆 앞·가운데·뒤와 앞뒤 끝이 뒤따른다.</summary>
        public static void Collect(Car car, NavMeshQueryFilter filter, List<Vector3> into)
        {
            into.Clear();
            Transform t = car.transform;
            Vector3 dropOff = car.DropOffPosition;
            Vector3 local = t.InverseTransformPoint(dropOff);

            Add(dropOff, filter, into);
            Add(t.TransformPoint(new Vector3(-local.x, local.y, local.z)), filter, into);

            NavMeshObstacle obstacle = car.GetComponentInChildren<NavMeshObstacle>();
            if (obstacle == null || obstacle.shape != NavMeshObstacleShape.Box)
            {
                return;
            }

            Transform o = obstacle.transform;
            Vector3 c = obstacle.center;
            Vector3 e = obstacle.size * 0.5f;
            float side = e.x + Margin;
            float end = e.z + Margin;

            float[] alongs = { -e.z * 0.6f, 0f, e.z * 0.6f };
            for (int i = 0; i < alongs.Length; i++)
            {
                Add(o.TransformPoint(c + new Vector3(side, 0f, alongs[i])), filter, into);
                Add(o.TransformPoint(c + new Vector3(-side, 0f, alongs[i])), filter, into);
            }

            Add(o.TransformPoint(c + new Vector3(0f, 0f, end)), filter, into);
            Add(o.TransformPoint(c + new Vector3(0f, 0f, -end)), filter, into);
        }

        /// <summary>차 몸체(NavMeshObstacle 상자) 표면에서 <paramref name="point"/>에 가장 가까운 점(높이는 point 그대로).
        /// 상자가 없으면 false.</summary>
        public static bool TryClosestPointOnBody(Car car, Vector3 point, out Vector3 surface)
        {
            surface = point;
            NavMeshObstacle obstacle = car.GetComponentInChildren<NavMeshObstacle>();
            if (obstacle == null || obstacle.shape != NavMeshObstacleShape.Box)
            {
                return false;
            }

            Transform o = obstacle.transform;
            Vector3 local = o.InverseTransformPoint(point) - obstacle.center;
            Vector3 e = obstacle.size * 0.5f;
            var clamped = new Vector3(Mathf.Clamp(local.x, -e.x, e.x), local.y, Mathf.Clamp(local.z, -e.z, e.z));

            // 상자 안이면 가장 가까운 옆면으로 밀어낸다.
            if (Mathf.Abs(local.x) < e.x && Mathf.Abs(local.z) < e.z)
            {
                if (e.x - Mathf.Abs(local.x) < e.z - Mathf.Abs(local.z))
                {
                    clamped.x = Mathf.Sign(local.x == 0f ? 1f : local.x) * e.x;
                }
                else
                {
                    clamped.z = Mathf.Sign(local.z == 0f ? 1f : local.z) * e.z;
                }
            }

            surface = o.TransformPoint(clamped + obstacle.center);
            surface.y = point.y;
            return true;
        }

        /// <summary>차 몸체(NavMeshObstacle 상자) 바깥면까지의 수평 거리(m). 상자 안이면 0.
        /// 상자가 없으면 차 원점까지의 거리를 돌려준다.</summary>
        public static float DistanceToBody(Car car, Vector3 point)
        {
            NavMeshObstacle obstacle = car.GetComponentInChildren<NavMeshObstacle>();
            if (obstacle == null || obstacle.shape != NavMeshObstacleShape.Box)
            {
                Vector3 d = point - car.transform.position;
                d.y = 0f;
                return d.magnitude;
            }

            Transform o = obstacle.transform;
            Vector3 local = o.InverseTransformPoint(point) - obstacle.center;
            Vector3 e = obstacle.size * 0.5f;
            Vector3 lossy = o.lossyScale;
            float dx = Mathf.Max(0f, Mathf.Abs(local.x) - e.x) * lossy.x;
            float dz = Mathf.Max(0f, Mathf.Abs(local.z) - e.z) * lossy.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static void Add(Vector3 point, NavMeshQueryFilter filter, List<Vector3> into)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, Snap, filter))
            {
                into.Add(hit.position);
            }
        }
    }
}
