using System.Collections.Generic;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>지금 씬에 나와 있는 차의 센서 목록. 센서끼리 서로를 찾고, 스폰하는 쪽이 자리가 비었는지 묻는다.</summary>
    public static class CarTraffic
    {
        private static readonly List<ICarTrafficSensor> _sensors = new();

        public static IReadOnlyList<ICarTrafficSensor> Sensors => _sensors;

        public static void Register(ICarTrafficSensor sensor)
        {
            if (sensor != null && !_sensors.Contains(sensor))
            {
                _sensors.Add(sensor);
            }
        }

        public static void Unregister(ICarTrafficSensor sensor)
        {
            _sensors.Remove(sensor);
        }

        /// <summary>아직 씬에 나와 있는 차인지. 추월하던 상대가 풀로 돌아갔는지 확인할 때 쓴다.</summary>
        public static bool IsActive(ICarTrafficSensor sensor) => sensor != null && _sensors.Contains(sensor);

        /// <summary>이 원 안에 차가 하나도 없는지. 스폰 지점에 차가 겹쳐 나오지 않게 먼저 확인한다.</summary>
        public static bool IsAreaClear(Vector3 point, float radius)
        {
            for (int i = 0; i < _sensors.Count; i++)
            {
                if (_sensors[i].Overlaps(point, radius))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>from에서 to까지 반경 radius로 쓸고 지나가는 띠 안에 차가 없는지. ignore는 검사에서 뺀다(보통 자기 차).
        /// 주차 자리로 들어가는 직선이 다른 차에 막혔는지 볼 때 쓴다.</summary>
        public static bool IsSegmentClear(Vector3 from, Vector3 to, float radius, ICarTrafficSensor ignore = null)
        {
            const float step = 0.5f;

            Vector3 delta = to - from;
            delta.y = 0f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / step));

            for (int s = 0; s < _sensors.Count; s++)
            {
                ICarTrafficSensor sensor = _sensors[s];
                if (ReferenceEquals(sensor, ignore))
                {
                    continue;
                }

                // 멀리 있는 차는 점마다 따지지 않고 바로 거른다.
                if (PlanarDistanceToSegment(sensor.Center, from, to) > radius + sensor.BoundingRadius)
                {
                    continue;
                }

                for (int i = 0; i <= steps; i++)
                {
                    if (sensor.Overlaps(Vector3.Lerp(from, to, (float)i / steps), radius))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>높이를 무시한 점과 선분 사이 거리.</summary>
        public static float PlanarDistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector2 p = new(point.x, point.z);
            Vector2 s = new(a.x, a.z);
            Vector2 e = new(b.x, b.z);
            Vector2 d = e - s;

            float lengthSqr = d.sqrMagnitude;
            float t = lengthSqr > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - s, d) / lengthSqr) : 0f;
            return Vector2.Distance(p, s + d * t);
        }

        // 도메인 리로드를 끈 에디터에서는 static이 플레이 사이에 남는다. 죽은 센서가 섞이지 않게 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => _sensors.Clear();
    }
}
