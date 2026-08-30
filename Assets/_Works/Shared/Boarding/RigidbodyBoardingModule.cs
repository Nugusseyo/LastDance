using _Works.JJH._02_Scripts.Agents.Modules;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.Shared.Boarding
{
    /// <summary>
    /// Rigidbody로 직접 움직이는 에이전트(플레이어)의 탑승.
    /// 좌석에 붙은 동안 물리를 잠가 차와 충돌해 밀려나지 않게 한다.
    /// </summary>
    public sealed class RigidbodyBoardingModule : BoardingModule
    {
        [Tooltip("비워두면 소유자 계층에서 찾아 쓴다.")]
        [SerializeField] private Rigidbody body;

        [Tooltip("탑승 중 꺼둘 콜라이더. 비워두면 콜라이더는 건드리지 않는다.")]
        [SerializeField] private Collider[] collidersToDisable;

        private IMover _mover;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            if (owner == null)
            {
                return;
            }

            if (body == null)
            {
                body = owner.GetComponentInChildren<Rigidbody>();
            }

            _mover = owner.GetModule<IMover>();
        }

        protected override void OnBoarded()
        {
            // 관성이 남아 있으면 좌석에 붙은 뒤에도 한 프레임 밀린다.
            _mover?.Stop();

            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            SetCollidersEnabled(false);
        }

        protected override void OnUnboarded(Vector3 landingPosition)
        {
            SetCollidersEnabled(true);

            if (body == null)
            {
                return;
            }

            body.isKinematic = false;
            body.position = landingPosition;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void SetCollidersEnabled(bool value)
        {
            if (collidersToDisable == null)
            {
                return;
            }

            for (int i = 0; i < collidersToDisable.Length; i++)
            {
                if (collidersToDisable[i] != null)
                {
                    collidersToDisable[i].enabled = value;
                }
            }
        }
    }
}
