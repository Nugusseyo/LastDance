namespace _Works.CJW.Scripts.Customers.Patience
{
    /// <summary>손님의 인내심. 플레이어를 기다리는 상태(주유 대기·요구 대기)가 기다리기 시작할 때 <see cref="Begin"/>으로 켜고,
    /// 끝날 때 <see cref="End"/>로 끈다. 인내심은 시간이 지나며 줄 뿐이고, 바닥났을 때 무엇을 할지(평판 감소·떠나기)는 기다리는 상태가 정한다.</summary>
    public interface ICustomerPatience
    {
        /// <summary>지금 무언가를 기다리며 인내심이 줄고 있는지.</summary>
        bool IsWaiting { get; }

        /// <summary>남은 인내심. 1이 가득, 0이 바닥이다. 기다리지 않을 때는 1이다.</summary>
        float Normalized { get; }

        /// <summary><paramref name="seconds"/>초 동안 참는 기다림을 시작한다. 이미 기다리던 중이면 처음부터 다시 잰다.
        /// 0 이하면(끝없이 기다리는 손님) 구현이 정한 기본 시간만큼 줄고, 바닥난 뒤에도 기다림은 이어진다.</summary>
        void Begin(float seconds);

        /// <summary>기다림을 끝내고 인내심을 가득 채운다. 기다리지 않던 중이면 아무것도 하지 않는다.</summary>
        void End();
    }
}
