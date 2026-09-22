namespace _Works.CJW.Scripts.Customers.Movement
{
    /// <summary>루트 모션을 받아 몸통에 반영하는 쪽. <see cref="RootMotionRelay"/>가 이것만 알면 되므로
    /// 중계는 어떤 이동 구현에도 그대로 붙는다.</summary>
    public interface IRootMotionReceiver
    {
        /// <summary>애니메이터가 이번 프레임에 만든 루트 모션을 반영한다. OnAnimatorMove 안에서만 불러야 한다.</summary>
        void ApplyRootMotion();
    }
}
