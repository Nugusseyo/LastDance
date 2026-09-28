using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>주유소 앞 도로를 그냥 지나가는 차들. 손님을 태우지 않고 스폰 지점에서 퇴장 지점까지 달려간 뒤 사라진다.
    /// 어느 차가 들어올지는 <see cref="VisitDirector"/>가 정하고, 여기는 지나가는 차의 수명(출발·도착·반납)만 맡는다.</summary>
    public sealed class PassingTraffic
    {
        private sealed class Passing
        {
            public Car Car;
            public float Elapsed;
        }

        private readonly List<Passing> _cars = new();

        /// <summary>다 지나간 차를 틱에서 빼고 풀로 돌려보내는 방법. 차를 꺼낸 쪽이 맡는다.</summary>
        private readonly Action<Car> _release;

        public int Count => _cars.Count;

        public PassingTraffic(Action<Car> release)
        {
            _release = release;
        }

        /// <summary>스폰 지점에 놓인 차를 퇴장 지점으로 출발시킨다. 틱 등록은 부른 쪽이 먼저 한다.</summary>
        public void Add(Car car, Vector3 exit)
        {
            car.MoveTo(exit);
            _cars.Add(new Passing { Car = car });
        }

        /// <summary>퇴장 지점에 닿은 차를 치운다. 앞이 막혀 한계 시간 안에 못 닿은 차도 치운다 — 남겨 두면 도로를 영영 막는다.</summary>
        public void Tick(float dt, Vector3 exit, float exitRadius, float maxLifetime)
        {
            for (int i = _cars.Count - 1; i >= 0; i--)
            {
                Passing passing = _cars[i];
                passing.Elapsed += dt;

                Car car = passing.Car;
                if (car == null)
                {
                    _cars.RemoveAt(i);
                    continue;
                }

                Vector3 delta = car.transform.position - exit;
                delta.y = 0f;
                bool arrived = car.IsArrived || delta.sqrMagnitude <= exitRadius * exitRadius;

                if (!arrived && passing.Elapsed < maxLifetime)
                {
                    continue;
                }

                if (!arrived)
                {
                    Debug.LogWarning($"[PassingTraffic] {car.name}이(가) {maxLifetime}초 안에 퇴장 지점에 닿지 못해 치웁니다. 위치 {car.transform.position}", car);
                }

                _cars.RemoveAt(i);
                _release(car);
            }
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
