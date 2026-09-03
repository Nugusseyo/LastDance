using UnityEngine;

namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>
    /// 한 번에 한 명만 쓸 수 있는 지점. 주차 자리나 좌석처럼 배정이 필요한 곳에 쓴다.
    /// 가게 입구처럼 여럿이 동시에 지나가도 되는 지점은 MapPosition을 그대로 쓰면 된다.
    /// </summary>
    public class RentableMapPosition : MapPosition
    {
        /// <summary>대여 중인지 여부. 상태를 바꾸는 것은 MapDataSo뿐이다.</summary>
        public bool IsOccupied { get; private set; }

        public override bool IsAvailable => !IsOccupied;

        internal void SetOccupied(bool value)
        {
            IsOccupied = value;
        }

        protected override void OnDisable()
        {
            // 빌린 채로 사라지면 다음에 켜졌을 때 점유 상태가 남는다.
            SetOccupied(false);
            base.OnDisable();
        }

        protected override Color GetGizmoColor()
        {
            if (!Application.isPlaying)
            {
                return base.GetGizmoColor();
            }

            return IsOccupied
                ? new Color(1f, 0.4f, 0.3f, 0.5f)
                : new Color(0.3f, 1f, 0.5f, 0.5f);
        }
    }
}
