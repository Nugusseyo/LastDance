using System;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Ragdoll
{
    /// <summary>애니메이션을 끄고 물리로 쓰러지는 몸. 누가 쓰러뜨리는지(차·폭발·주먹)는 모른다 — 날아갈 속도나 충격만 받는다.</summary>
    public interface IRagdoll
    {
        /// <summary>지금 물리로 쓰러져 있는지.</summary>
        bool IsActive { get; }

        /// <summary>쓰러졌다가 다시 일어섰다. 쓰러져 멈춰 있던 행동이 이걸 보고 이어 간다.</summary>
        event Action Recovered;

        /// <summary>애니메이션을 끄고 몸 전체에 이 속도를 줘 날려 보낸다. 이미 쓰러져 있으면 속도만 더한다.
        /// <paramref name="stayDown"/>를 켜면 저절로 일어서지 않는다 — 죽은 몸처럼 누가 치울 때까지 누워 있다.</summary>
        void Activate(Vector3 launchVelocity, bool stayDown = false);

        /// <summary>애니메이션을 끄고 <paramref name="hitPoint"/>에서 가장 가까운 뼈에 <paramref name="impulse"/>(N·s)를 준다.
        /// 맞은 부위만 먼저 튕겨 나가고 나머지 몸은 관절에 끌려가 주먹·무기에 맞아 쓰러지는 느낌이 난다.</summary>
        void ActivateAt(Vector3 hitPoint, Vector3 impulse, bool stayDown = false);

        /// <summary>쓰러져 있는 동안 이 콜라이더들과는 부딪히지 않는다. 몸을 친 쪽처럼 이미 속도로 반영한 상대를 뺄 때 쓴다.
        /// 일어서면 풀린다.</summary>
        void IgnoreWhileDown(Collider[] colliders);

        /// <summary>쓰러진 자리에서 일어선다. 몸이 떨어진 곳으로 본체를 옮기고 애니메이션을 다시 켠다.</summary>
        void Recover();
    }
}
