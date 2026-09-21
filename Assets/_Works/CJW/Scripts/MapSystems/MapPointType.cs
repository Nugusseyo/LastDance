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
    }
}
