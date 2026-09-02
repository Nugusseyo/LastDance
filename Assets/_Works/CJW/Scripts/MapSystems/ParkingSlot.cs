namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>
    /// 차 한 대가 설 수 있는 자리. 씬에 놓고 MapData만 물려주면 스스로 등록한다.
    /// 자리를 늘리려면 이 오브젝트를 복제해 원하는 위치에 놓기만 하면 된다.
    ///
    /// 위치·회전·등록·점유는 전부 부모가 처리한다. 여기는 종류만 밝힌다.
    /// 종류가 고정이라 인스펙터에서 고를 것이 없고, 이미 배치해둔 자리들도 그대로 동작한다.
    /// </summary>
    public class ParkingSlot : RentableMapPosition
    {
        public override MapPointType Type => MapPointType.ParkingSlot;
    }
}
