using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>다른 차와의 간격을 재고, 막히면 옆으로 비켜 지나갈 방향을 정하는 센서.
    /// 경로 자체는 바꾸지 않는다. 이동 모듈이 속도를 줄이고 목표점을 옆으로 미는 데 이 값을 쓴다.</summary>
    public interface ICarTrafficSensor : IModule
    {
        /// <summary>지금 이 차가 양보하고 있는 차. 없으면 null. 서로 양보해 멈추는 교착을 풀 때 쓴다.</summary>
        ICarTrafficSensor Blocker { get; }

        /// <summary>진행 방향 차로 안에서 가장 가까운 차까지의 거리(m). range 안에 없으면 float.PositiveInfinity.
        /// 매 프레임 한 번 불러야 추월 판단도 함께 진행된다. allowBypass가 false면 추월을 시작하지 않고, 하던 추월은 접는다.
        /// travelDirection은 차가 실제로 나아가려는 방향(조향 목표점 쪽)이다. 차로를 이 방향으로 긋는다. 후진 중에는 무시한다.</summary>
        float ClearDistance(bool reversing, float range, bool allowBypass, Vector3 travelDirection);

        /// <summary>추월 중일 때 경로 목표점을 옆으로 밀 양(월드 벡터). 추월하지 않으면 Vector3.zero.</summary>
        Vector3 AvoidanceOffset { get; }

        /// <summary>하던 추월을 바로 접는다. 차가 멈춰 설 때 불러, 다음 출발에 이전 추월이 섞이지 않게 한다.</summary>
        void CancelBypass();

        /// <summary>차체가 이 원과 겹치는지. 스폰 지점이 비었는지 확인할 때 쓴다.</summary>
        bool Overlaps(Vector3 point, float radius);

        /// <summary>차체 바닥 사각형의 네 모서리를 월드 좌표로 채운다. 다른 센서가 이걸 읽어 자기 차로와 겹치는지 본다.</summary>
        void GetCorners(Vector3[] corners);

        /// <summary>차체 중심과 대략적인 반경. 멀리 있는 차를 빨리 걸러낼 때 쓴다.</summary>
        Vector3 Center { get; }

        /// <summary>실제 이동 속도(월드). 옆에서 끼어드는 차의 다음 자리를 예측할 때 쓴다.</summary>
        Vector3 Velocity { get; }
        float BoundingRadius { get; }

        /// <summary>교착을 풀 때 누가 먼저 갈지 정하는 값. 차마다 달라야 한다.</summary>
        int Priority { get; }
    }
}
