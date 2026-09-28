using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>주유소 앞 도로를 그냥 지나가는 차들. 손님을 태우지 않고 스폰 지점에서 퇴장 지점까지 한 줄을 따라 달린 뒤 사라진다.
    /// 들어올 일이 없으니 경로 찾기·조향·추월 없이 직접 옮기고, 같은 차선 앞에 차가 있으면 속도만 줄여 따라간다.
    /// 어느 차가 들어올지는 <see cref="VisitDirector"/>가 정하고, 여기는 지나가는 차의 수명(출발·주행·반납)만 맡는다.</summary>
    public sealed class PassingTraffic
    {
        private sealed class Passing
        {
            public Car Car;
            public ICarTrafficSensor Sensor;
            public Vector3 From;
            public Vector3 Direction;
            public float Length;
            public float Distance;
            public float Speed;
            public float MaxSpeed;
            public float Elapsed;
        }

        /// <summary>출발할 때의 가속(m/s²).</summary>
        private const float Accel = 6f;

        /// <summary>앞차 때문에 줄일 때의 감속(m/s²). 이 감속으로 설 수 있는 속도까지만 낸다.</summary>
        private const float ComfortBrake = 3f;

        /// <summary>앞차가 급히 설 때 낼 수 있는 최대 감속(m/s²).</summary>
        private const float MaxBrake = 8f;

        /// <summary>앞차 범퍼와 둘 최소 간격(m).</summary>
        private const float MinGap = 2f;

        /// <summary>차선 중심선에서 이 거리(m) 안에 중심이 든 차를 같은 차선의 앞차로 본다. 옆 차선(중심 간격 4m) 차는 빼고, 걸쳐 끼어드는 차는 잡히게.</summary>
        private const float LaneHalfWidth = 1.8f;

        /// <summary>CarDataSO에 속도가 없을 때의 최고 속도(m/s).</summary>
        private const float DefaultMaxSpeed = 11f;

        private readonly List<Passing> _cars = new();

        /// <summary>다 지나간 차를 틱에서 빼고 풀로 돌려보내는 방법. 차를 꺼낸 쪽이 맡는다.</summary>
        private readonly Action<Car> _release;

        public int Count => _cars.Count;

        public PassingTraffic(Action<Car> release)
        {
            _release = release;
        }

        /// <summary>스폰 지점에 놓인 차를 출발시킨다. 이동 모듈 대신 여기서 직접 옮기므로 차를 <see cref="Car.SetGliding"/> 상태로 둔다.</summary>
        public void Add(Car car, Vector3 from, Vector3 to)
        {
            Vector3 line = to - from;
            line.y = 0f;
            if (line.sqrMagnitude < 1f)
            {
                _release(car);
                return;
            }

            car.SetGliding(true);
            _cars.Add(new Passing
            {
                Car = car,
                Sensor = car.GetModule<ICarTrafficSensor>(),
                From = from,
                Direction = line.normalized,
                Length = line.magnitude,
                MaxSpeed = car.Data != null && car.Data.MoveSpeed > 0f ? car.Data.MoveSpeed : DefaultMaxSpeed,
            });
        }

        /// <summary>차선을 따라 한 틱 옮긴다. 퇴장 지점 반경 안에 들면 치우고, 앞이 막혀 한계 시간 안에 못 닿은 차도 치운다.</summary>
        public void Tick(float dt, float exitRadius, float maxLifetime)
        {
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                Passing passing = _cars[i];
                if (passing.Car == null)
                {
                    _cars.RemoveAt(i);
                    continue;
                }

                passing.Elapsed += dt;

                float gap = GapToLeader(passing);
                float target = passing.MaxSpeed;
                if (gap < float.PositiveInfinity)
                {
                    target = Mathf.Min(target, Mathf.Sqrt(Mathf.Max(0f, 2f * ComfortBrake * (gap - MinGap))));
                }

                passing.Speed = target > passing.Speed
                    ? Mathf.Min(target, passing.Speed + Accel * dt)
                    : Mathf.Max(target, passing.Speed - MaxBrake * dt);
                passing.Distance += passing.Speed * dt;

                Vector3 position = passing.From + passing.Direction * passing.Distance;
                passing.Car.Glide(position, Quaternion.LookRotation(passing.Direction), passing.Speed);

                bool arrived = passing.Distance >= passing.Length - exitRadius;
                if (!arrived && passing.Elapsed < maxLifetime)
                {
                    continue;
                }

                if (!arrived)
                {
                    Debug.LogWarning($"[PassingTraffic] {passing.Car.name}이(가) {maxLifetime}초 안에 퇴장 지점에 닿지 못해 치웁니다. 위치 {position}", passing.Car);
                }

                _cars.RemoveAt(i);
                _release(passing.Car);
            }
        }

        /// <summary>같은 차선 앞쪽 가장 가까운 차의 뒤 범퍼까지 거리(m). 지나가는 차뿐 아니라 도로를 타는 방문 차, 도로로 끼어드는 차도 본다.</summary>
        private static float GapToLeader(Passing passing)
        {
            ICarTrafficSensor self = passing.Sensor;
            Vector3 center = self != null ? self.Center : passing.Car.transform.position;
            float myHalf = self != null ? self.BoundingRadius * 0.9f : 3f;
            Vector3 right = Vector3.Cross(Vector3.up, passing.Direction);

            float best = float.PositiveInfinity;
            IReadOnlyList<ICarTrafficSensor> sensors = CarTraffic.Sensors;
            for (int i = 0; i < sensors.Count; i++)
            {
                ICarTrafficSensor other = sensors[i];
                if (ReferenceEquals(other, self))
                {
                    continue;
                }

                Vector3 rel = other.Center - center;
                rel.y = 0f;
                float ahead = Vector3.Dot(rel, passing.Direction);
                if (ahead <= 0f || Mathf.Abs(Vector3.Dot(rel, right)) > LaneHalfWidth)
                {
                    continue;
                }

                float gap = ahead - myHalf - other.BoundingRadius * 0.9f;
                best = Mathf.Min(best, Mathf.Max(0f, gap));
            }

            return best;
        }

        /// <summary>지나가던 차를 모두 치운다. 방문 감독이 꺼질 때 부른다.</summary>
        public void Clear()
        {
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                if (_cars[i].Car != null)
                {
                    _release(_cars[i].Car);
                }
            }

            _cars.Clear();
        }
    }
}
