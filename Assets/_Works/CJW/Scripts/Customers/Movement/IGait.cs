using DevLib.AnimatorSystem;

namespace _Works.CJW.Scripts.Customers.Movement
{
    /// <summary>걸음걸이의 변조. 이동 모듈은 "어떻게 걷는지"를 이 계약으로만 묻고, 취한 걸음인지 절뚝이는 걸음인지는 구현이 정한다.
    /// 경로 자체는 건드리지 않는다 — 목적지와 길찾기는 그대로 두고 보이는 걸음만 비튼다.</summary>
    public interface IGait
    {
        /// <summary>지금 변조를 걸고 있는지. 꺼져 있으면 이동 모듈은 자기 기본 걷기를 쓴다.</summary>
        bool IsActive { get; }

        /// <summary>걸을 때 쓸 클립. null이면 이동 모듈의 기본 걷기 클립을 그대로 쓴다.</summary>
        HashDataSO WalkClip { get; }

        /// <summary>진행 방향에 더할 좌우 각도(도). 몸이 가려는 쪽에서 이만큼 비껴 보게 만들어 비틀거리는 인상을 준다.</summary>
        float SwayAngle { get; }

        /// <summary>변조를 켜고 끈다. 술이 깨거나 다리를 고쳤을 때처럼 방문 도중에 바뀔 수 있다.</summary>
        void SetActive(bool value);
    }
}
