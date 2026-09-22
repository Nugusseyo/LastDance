namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>맵 지점의 종류. 씬과 프리팹에 정수로 직렬화되므로 번호는 항상 명시하고, 새 항목은 뒤에만 추가하며, 지운 번호는 재사용하지 않는다.</summary>
    public enum MapPointType
    {
        None = 0,
        ParkingSlot = 1,
        OilDispenser = 2,
        Counter = 3,
        Table = 4,
        WaitingLine = 5,
        Exit = 6,

        /// <summary>손님끼리 싸우러 모이는 자리. 짝이 맺어지면 둘의 중간에서 가장 가까운 여기로 함께 온다.</summary>
        FightArea = 7,

        /// <summary>정비 차고. 바퀴 교체처럼 차를 세워 두고 손봐야 하는 일이 여기서 일어난다.</summary>
        Garage = 8,

        /// <summary>자판기 앞. 때리는 손님은 이 지점을 찾아가고, 실제로 맞는 쪽은 여기 붙은 IVandalTarget이 맡는다.</summary>
        VendingMachine = 9,

        /// <summary>입구 도로 옆. 춤추는 손님처럼 길가에서 소란을 피우는 손님이 여기로 온다.</summary>
        Roadside = 10,

        /// <summary>입구를 가로막을 때 차를 대는 자리. 여기 회전이 곧 '막지 않았을 때의' 방향이라, 막는 차는 여기서 90도 틀어 선다.</summary>
        Entrance = 11,

        /// <summary>뺑뺑 도는 차가 훑고 다니는 경유지. 두 개 이상 놓아야 순회가 된다.</summary>
        Patrol = 12,
    }
}
