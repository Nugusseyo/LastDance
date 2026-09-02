using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.Shared.Boarding
{
    /// <summary>
    /// 좌석에 붙이고 떼는 공통 절차. 손님과 플레이어가 이 코드를 함께 쓴다.
    /// 이동 수단을 멈추고 되살리는 방법만 파생 클래스가 채운다.
    /// </summary>
    public abstract class BoardingModule : AbstractModule, IBoardable
    {
        public bool IsBoarded { get; private set; }

        /// <summary>실제로 좌석에 붙일 트랜스폼. 모듈이 자식에 달려 있어도 몸통이 움직여야 한다.</summary>
        protected Transform Body => _owner != null ? _owner.transform : transform;

        public void Board(Transform seat)
        {
            if (IsBoarded || seat == null)
            {
                return;
            }

            IsBoarded = true;

            // 물리·내비를 먼저 끊어야 좌석에 붙이는 순간 튀지 않는다.
            OnBoarded();

            Transform body = Body;
            body.SetParent(seat, false);
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
        }

        public void Unboard(Vector3 landingPosition)
        {
            if (!IsBoarded)
            {
                return;
            }

            IsBoarded = false;

            Transform body = Body;
            body.SetParent(null, true);
            body.position = landingPosition;

            OnUnboarded(landingPosition);
        }

        public void ResetBoarding()
        {
            if (!IsBoarded)
            {
                return;
            }

            Unboard(Body.position);
        }

        /// <summary>좌석에 붙기 직전. 이동 제어를 끊는다.</summary>
        protected abstract void OnBoarded();

        /// <summary>좌석에서 떨어진 직후. 이동 제어를 되살린다.</summary>
        protected abstract void OnUnboarded(Vector3 landingPosition);
    }
}
