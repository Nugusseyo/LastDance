using UnityEngine;

namespace _Works.Shared.Boarding
{
    /// <summary>
    /// 좌석에 탈 수 있는 대상. 손님이든 플레이어든 태우는 쪽은 이 계약만 알면 된다.
    /// 이동 수단을 어떻게 끄고 켜는지는 구현체가 안다.
    /// </summary>
    public interface IBoardable
    {
        /// <summary>좌석에 붙어 있는 동안 true. 이 동안에는 스스로 이동하지 않는다.</summary>
        bool IsBoarded { get; }

        /// <summary>좌석에 붙고 이동 제어를 넘긴다.</summary>
        void Board(Transform seat);

        /// <summary>좌석에서 떼어내 지정 위치에 세운다. 이후 어디로 갈지는 부르는 쪽이 정한다.</summary>
        void Unboard(Vector3 landingPosition);

        /// <summary>풀 반납처럼 상태를 강제로 되돌려야 할 때. 타고 있지 않으면 아무 일도 하지 않는다.</summary>
        void ResetBoarding();
    }
}
