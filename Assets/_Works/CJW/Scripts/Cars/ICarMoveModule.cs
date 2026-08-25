using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>
    /// 차량의 이동 수단. NavMesh, 스플라인 등 구현은 모듈이 정한다.
    /// </summary>
    public interface ICarMoveModule : IModule
    {
        bool IsArrived { get; }

        void MoveTo(Vector3 destination);

        /// <summary>
        /// approachFrom을 먼저 지나 destination에 닿는다. 마지막 구간을 직선으로 만들어
        /// 도착했을 때 방향까지 맞추려는 용도다. 구현이 지원하지 않으면 그냥 destination으로 가도 된다.
        /// </summary>
        void MoveTo(Vector3 destination, Vector3 approachFrom);
        void Stop();

        /// <summary>CarDataSO의 값을 이동 수단에 반영한다. moveSpeed가 0 이하면 프리팹 값을 그대로 쓴다.</summary>
        void ApplyStats(float moveSpeed, float arriveThreshold);
    }
}
