using UnityEngine;

namespace _Works.Shared.Combat
{
    /// <summary>플레이어의 주먹·무기·던진 물건에 맞을 수 있는 대상. 때리는 쪽은 맞은 콜라이더에서
    /// <c>GetComponentInParent&lt;IHittable&gt;()</c>로 이걸 찾아 <see cref="TakeHit"/>만 부른다 — 맞은 쪽이 누구인지는 몰라도 된다.</summary>
    public interface IHittable
    {
        /// <summary>지금 맞을 수 있는지. 차에 타 있거나 이미 쓰러져 있으면 false다.</summary>
        bool CanBeHit { get; }

        /// <summary>한 대 맞는다. <see cref="CanBeHit"/>가 false면 무시한다.</summary>
        void TakeHit(HitInfo hit);
    }

    /// <summary>한 번의 타격. 3D 공간 기준이다.</summary>
    public readonly struct HitInfo
    {
        /// <summary>깎을 체력.</summary>
        public readonly float Damage;

        /// <summary>때린 방향. 맞은 쪽이 이 방향으로 밀려난다. 정규화하지 않아도 된다.</summary>
        public readonly Vector3 Direction;

        /// <summary>밀어내는 세기(m/s). 0이면 밀리지 않는다.</summary>
        public readonly float Force;

        /// <summary>때린 쪽. 맞은 쪽이 반격하거나 도망칠 때 이 대상을 본다. 없으면 null.</summary>
        public readonly GameObject Attacker;

        public HitInfo(float damage, Vector3 direction, float force = 0f, GameObject attacker = null)
        {
            Damage = damage;
            Direction = direction;
            Force = force;
            Attacker = attacker;
        }
    }
}
