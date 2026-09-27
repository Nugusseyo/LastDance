using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>달리는 차에 치일 수 있는 것. 차는 이 목록만 보고 자기 차체와 겹치는지 따진다 — 누가 치였는지는 몰라도 된다.</summary>
    public interface ICarHittable
    {
        /// <summary>바닥 기준 위치. 높이는 무시한다.</summary>
        Vector3 HitPosition { get; }

        /// <summary>몸의 반경(m). 차체 사각형과 이 원이 겹치면 치인 것이다.</summary>
        float HitRadius { get; }

        /// <summary>지금 치일 수 있는지. 차에 타 있거나 꺼져 있으면 false.</summary>
        bool CanBeHit { get; }

        /// <summary>차에 치였을 때 차가 부른다.</summary>
        void OnHitByCar(CarHitInfo hit);
    }

    /// <summary>한 번의 치임. 차, 치일 때의 속도, 부딪힌 곳.</summary>
    public readonly struct CarHitInfo
    {
        public readonly Car Car;
        public readonly ICarHittable Target;
        public readonly Vector3 Velocity;
        public readonly Vector3 Point;

        public float Speed => new Vector3(Velocity.x, 0f, Velocity.z).magnitude;

        public CarHitInfo(Car car, ICarHittable target, Vector3 velocity, Vector3 point)
        {
            Car = car;
            Target = target;
            Velocity = velocity;
            Point = point;
        }
    }
}
