namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>
    /// 맵 지점의 종류. 씬에 놓인 MapPosition이 자기 종류를 이 값으로 밝힌다.
    ///
    /// 규칙 두 가지를 반드시 지킬 것.
    /// 1. 번호는 항상 명시적으로 적고, 새 항목은 뒤에만 추가한다.
    /// 2. 지운 번호는 영원히 재사용하지 않는다.
    /// enum은 씬과 프리팹에 정수로 직렬화되기 때문에, 중간에 값을 끼워넣으면
    /// 이미 배치된 모든 지점의 종류가 조용히 한 칸씩 밀린다.
    /// </summary>
    public enum MapPointType
    {
        None = 0,
        ParkingSlot = 1,
        ShopEntrance = 2,
        Counter = 3,
        Table = 4,
        Exit = 5,
    }
}
