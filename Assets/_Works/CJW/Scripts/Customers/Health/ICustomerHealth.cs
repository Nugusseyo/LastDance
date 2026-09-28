using System;
using _Works.Shared.Combat;

namespace _Works.CJW.Scripts.Customers.Health
{
    /// <summary>손님의 체력. 맞는 창구(<see cref="IHittable"/>)와 체력 수치를 함께 든다.
    /// 체력이 바닥나면 죽는다 — 래그돌로 쓰러진 뒤 잠시 후 사라진다(풀로 돌아간다).</summary>
    public interface ICustomerHealth : IHittable
    {
        float MaxHealth { get; }

        float CurrentHealth { get; }

        /// <summary>체력이 바닥났는지. 죽은 손님은 더 맞지 않고, 사라질 때까지 누워 있다.</summary>
        bool IsDead { get; }

        /// <summary>한 대 맞았다. 체력이 바닥난 타격이어도 먼저 불린다.</summary>
        event Action<HitInfo> Damaged;

        /// <summary>체력이 바닥나 죽었다. 몸이 사라지기 전, 쓰러지는 순간에 불린다.</summary>
        event Action<HitInfo> Died;

        /// <summary>체력을 가득 채우고 죽음을 되돌린다. 풀에서 다시 꺼낼 때 부른다. 최대 체력도 프리팹 값으로 돌아간다.</summary>
        void ResetHealth();

        /// <summary>이번 방문 동안의 최대 체력을 정하고 가득 채운다(차 등급별 체력). 다음 <see cref="ResetHealth"/>에서 프리팹 값으로 돌아간다.</summary>
        void SetMaxHealth(float value);
    }
}
