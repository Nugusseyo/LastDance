namespace _Works.CJW.Scripts.Customers.Interaction
{
    /// <summary>손님이 플레이어에게 거는 요구의 종류. 프리팹에 정수로 직렬화되므로 번호는 항상 명시하고,
    /// 새 항목은 뒤에만 추가하며, 지운 번호는 재사용하지 않는다.</summary>
    public enum CustomerRequestType
    {
        None = 0,

        /// <summary>주유해 달라.</summary>
        Refuel = 1,

        /// <summary>바퀴를 갈아 달라.</summary>
        TireChange = 2,

        /// <summary>값을 깎아 달라.</summary>
        Negotiation = 3,
    }
}
