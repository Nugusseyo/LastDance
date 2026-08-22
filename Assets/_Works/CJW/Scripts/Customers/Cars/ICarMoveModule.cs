using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Cars
{
    /// <summary>
    /// 차량의 이동 수단. NavMesh, 스플라인 등 구현은 모듈이 정한다.
    /// </summary>
    public interface ICarMoveModule : IModule
    {
        bool IsArrived { get; }

        void MoveTo(Vector3 destination);
        void Stop();

        /// <summary>CarDataSO의 값을 이동 수단에 반영한다. moveSpeed가 0 이하면 프리팹 값을 그대로 쓴다.</summary>
        void ApplyStats(float moveSpeed, float arriveThreshold);
    }
}
