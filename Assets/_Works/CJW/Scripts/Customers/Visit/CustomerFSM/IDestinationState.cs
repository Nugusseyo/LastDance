using _Works.CJW.Scripts.MapSystems;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>어느 종류의 맵 지점으로 걸어가는 상태. 차에서 내릴 때 그 지점까지 가까운 쪽으로 내리게 하려고 목적지 종류만 알린다.</summary>
    public interface IDestinationState
    {
        /// <summary>걸어갈 지점의 종류. 지점 없이 움직이면 None.</summary>
        MapPointType Destination { get; }
    }
}
