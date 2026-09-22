using UnityEngine;
using UnityEngine.Events;

namespace _Works.CJW.Scripts.Customers.Interaction
{
    /// <summary>맞는 쪽의 기본 구현. 자판기나 차처럼 이미 자기 클래스를 가진 물건에 붙여 쓰면,
    /// 그 클래스를 고치지 않고도 손님의 타격을 받을 수 있다.
    /// 맞은 횟수만 세고 그 뒤의 일(아이템 드롭·파손 연출)은 이벤트로 넘긴다 — 여기서 게임 규칙을 정하지 않는다.</summary>
    public class VandalTarget : MonoBehaviour, IVandalTarget
    {
        [Tooltip("이만큼 맞으면 더 이상 때릴 수 없게 된다. 0이면 무한히 맞는다.")]
        [SerializeField, Min(0f)] private float endurance;

        [Tooltip("맞을 때마다 발생. 이펙트·소리·아이템 드롭을 여기에 건다.")]
        [SerializeField] private UnityEvent<Vector3> hit;

        [Tooltip("누적 타격이 내구도를 넘어섰을 때 한 번 발생.")]
        [SerializeField] private UnityEvent broken;

        private float _taken;

        /// <summary>지금까지 받은 타격의 합.</summary>
        public float Taken => _taken;

        /// <summary>내구도를 다 깎였는지. 내구도가 0(무한)이면 영영 false다.</summary>
        public bool IsBroken => endurance > 0f && _taken >= endurance;

        public bool CanTakeHit => isActiveAndEnabled && !IsBroken;

        public void TakeVandalHit(in VandalHit vandalHit)
        {
            if (!CanTakeHit)
            {
                return;
            }

            _taken += vandalHit.Power;

            hit?.Invoke(vandalHit.Point);

            if (IsBroken)
            {
                broken?.Invoke();
            }
        }

        /// <summary>고쳐졌을 때 부른다. 다시 맞을 수 있게 된다.</summary>
        public void Repair()
        {
            _taken = 0f;
        }
    }
}
