using _Works.Shared.Combat;

namespace _Works.CJW.Scripts.Customers.Health
{
    /// <summary>맞은 순간의 손맛 연출(멈칫·화면 반동·피·번쩍임·몸 젖힘·밀려남). 맞았는지·죽었는지는 체력이 정하고, 이건 보여 주기만 한다.
    /// 행동(움찔·반격·도망)은 상태 머신이 따로 맡는다.</summary>
    public interface ICustomerHitFeedback
    {
        /// <summary>한 대 맞은 연출을 튼다. <paramref name="lethal"/>이면 쓰러지는 타격이라 몸은 래그돌에 맡기고,
        /// 젖힘·밀려남은 건너뛴 채 멈칫·피·화면 반동을 크게 한다.</summary>
        void Play(HitInfo hit, bool lethal);

        /// <summary>하던 연출을 모두 접는다. 풀로 돌아갈 때 부른다.</summary>
        void Cancel();
    }
}
