using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>차량의 이동 수단. NavMesh, 스플라인 등 구현은 모듈이 정한다.</summary>
    public interface ICarMoveModule : IModule
    {
        /// <summary>목적지까지 실제로 닿는 경로를 들고 있는지. 부분 경로면 false다. 계산 중이면 판정을 미루고 true를 돌려준다.</summary>
        bool HasCompletePath { get; }

        bool IsArrived { get; }

        /// <summary>차체 정면 기준 속도(m/s). 후진이면 음수다. 바퀴를 굴릴 때 쓴다.</summary>
        float Speed { get; }

        /// <summary>앞바퀴 조향각(도). 양수면 오른쪽. 조향을 흉내 내지 않는 이동 수단은 0을 돌려준다.</summary>
        float SteerAngleDeg { get; }

        void MoveTo(Vector3 destination);

        /// <summary>approachFrom을 먼저 지나 destination에 닿는다. 구현이 지원하지 않으면 그냥 destination으로 가도 된다.</summary>
        void MoveTo(Vector3 destination, Vector3 approachFrom);
        void Stop();

        /// <summary>지금 목적지로 가기 전에 먼저 곧게 <paramref name="distance"/>m 물러선다. MoveTo 뒤에 부른다.
        /// 앞뒤 간격이 좁은 줄에서 먼저 떠나는 차가 앞차 옆으로 돌아 나갈 각을 벌 때 쓴다. 후진을 흉내 내지 않는 이동 수단은 무시해도 된다.</summary>
        void BackOff(float distance) { }

        /// <summary>제자리에서 앞뒤로 조금씩 오가며 <paramref name="direction"/> 쪽으로 차 방향을 튼다(N자 회전). 다 틀면 지금 목적지로 간다.
        /// MoveTo 뒤에 부른다. 앞뒤가 다른 차로 꽉 막혀 물러설 수도 없는 차가 옆으로 빠져나올 때 쓴다.</summary>
        void TurnInPlace(Vector3 direction) { }

        /// <summary>제자리 회전처럼 목적지를 향해 달리는 게 아닌 기동 중인지.</summary>
        bool IsManeuvering => false;

        /// <summary>경로·조향 없이 밖에서 정한 자리로 차를 옮긴다(도로를 지나가기만 하는 차). 바닥 높이는 이동 수단이 맞추고,
        /// <paramref name="speed"/>는 바퀴를 굴리는 데만 쓴다. 지원하지 않으면 false — 부른 쪽이 위치만 옮긴다.</summary>
        bool Glide(Vector3 position, Quaternion rotation, float speed) => false;

        /// <summary>CarDataSO의 값을 이동 수단에 반영한다. moveSpeed가 0 이하면 프리팹 값을 그대로 쓴다.</summary>
        void ApplyStats(float moveSpeed, float arriveThreshold);
    }
}
