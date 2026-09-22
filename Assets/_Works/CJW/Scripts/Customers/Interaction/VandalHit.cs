using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Interaction
{
    /// <summary>때린 한 번의 내용. 맞는 쪽이 필요로 할 만한 값만 담고, 때린 쪽의 타입은 노출하지 않는다.
    /// 구조체라 매 타격마다 할당이 생기지 않는다.</summary>
    public readonly struct VandalHit
    {
        /// <summary>때린 손님의 오브젝트. 누가 때렸는지 세거나 되받아치는 데 쓴다.</summary>
        public readonly GameObject Attacker;

        /// <summary>이번 타격의 세기. 의미(내구도·체력·확률)는 맞는 쪽이 정한다.</summary>
        public readonly float Power;

        /// <summary>맞은 지점. 이펙트를 띄울 자리로 쓴다.</summary>
        public readonly Vector3 Point;

        /// <summary>때린 방향(정규화). 넉백이나 이펙트 회전에 쓴다.</summary>
        public readonly Vector3 Direction;

        public VandalHit(GameObject attacker, float power, Vector3 point, Vector3 direction)
        {
            Attacker = attacker;
            Power = power;
            Point = point;
            Direction = direction;
        }
    }
}
