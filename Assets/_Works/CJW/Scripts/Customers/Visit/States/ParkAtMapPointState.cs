using System;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>배정받은 주차 자리 대신 정해진 종류의 지점에 차를 세운다. 입구를 가로로 막는 차와
    /// 차고 옆에 대는 차가 이 하나로 표현된다 — 다른 건 어느 지점에 몇 도로 서느냐뿐이다.
    /// <see cref="ArrivingState"/> 자리에 <see cref="MapSystems.CarDataSO"/>의 연출 덮어쓰기로 꽂아 쓴다.
    /// 주차 자리는 VisitDirector가 이미 빌려 둔 상태로 남는다. 자리 하나를 차지한 채 엉뚱한 데 서 있는 셈인데,
    /// 입구를 막는 차에게는 오히려 그쪽이 맞는 그림이라 되돌리지 않는다.</summary>
    [Serializable]
    public sealed class ParkAtMapPointState : VisitState
    {
        [Tooltip("설 자리를 물어볼 맵 데이터. 씬의 MapPosition들이 등록하는 그 에셋이다.")]
        [SerializeField] private MapDataSo mapData;

        [Tooltip("이 종류의 지점 중 가장 가까운 곳에 선다.")]
        [SerializeField] private MapPointType parkAt = MapPointType.Entrance;

        [Tooltip("지점의 방향에서 이만큼 틀어서 선다(도). 90이면 길을 가로로 막는다.")]
        [SerializeField] private float yawOffset = 90f;

        [Tooltip("이 단계에 머물 수 있는 한계 시간(초). 없으면 막힌 세션이 주차 자리를 영영 반납하지 않아 스폰까지 멈춘다.")]
        [SerializeField, Min(0f)] private float phaseTimeout = 45f;

        public override VisitPhase Phase => VisitPhase.Arriving;

        public override void Enter(VisitContext context)
        {
            if (mapData == null)
            {
                // 여기서 멈추면 방문이 굳는다. 원래 배정받은 자리로라도 보낸다.
                Debug.LogError($"[{nameof(ParkAtMapPointState)}] MapData를 지정해야 합니다. 배정받은 주차 자리로 대신 갑니다.", context.Car);
            }
            else if (mapData.TryGetNearest(parkAt, context.Car.transform.position, out MapPosition point))
            {
                context.ArrivalPoint = point.Position;
                context.ArrivalRotation = point.Rotation * Quaternion.Euler(0f, yawOffset, 0f);
            }
            else
            {
                Debug.LogWarning($"[{nameof(ParkAtMapPointState)}] 맵에 {parkAt} 지점이 없어 배정받은 주차 자리로 갑니다. 씬에 MapPosition을 놓아야 합니다.", context.Car);
            }

            // 막는 차는 어느 쪽에서 들어오든 같은 각도로 서야 한다. 뒤집힌 방향을 허용하지 않는 것이
            // 정상 주차(ArrivingState)와의 차이다 — 180도 돌아 서면 가로막기가 풀린다.
            context.TargetRotation = context.ArrivalRotation;
            context.Car.MoveTo(context.ArrivalPoint);
        }

        public override VisitPhase Tick(VisitContext context, float dt)
        {
            context.PhaseElapsed += dt;

            if (!context.Aligning)
            {
                if (!context.Car.IsArrived)
                {
                    // 경로가 자리에 닿지 않으면 기다려도 달라지지 않는다. 한계 시간을 채우지 않고 넘긴다.
                    bool unreachable = !context.Car.HasCompletePath;

                    if (!unreachable && context.PhaseElapsed < phaseTimeout)
                    {
                        return VisitPhase.Arriving;
                    }

                    Debug.LogWarning(unreachable
                            ? $"[{nameof(ParkAtMapPointState)}] {context.Car.name}의 경로가 {parkAt}에 닿지 않습니다. 서 있는 자리에서 그대로 진행합니다."
                            : $"[{nameof(ParkAtMapPointState)}] {context.Car.name}이(가) {phaseTimeout}초 안에 자리에 들어가지 못해 그대로 진행합니다.",
                        context.Car);
                }

                // NavMesh가 회전을 되돌리지 않도록 먼저 멈춘 뒤에 방향을 맞춘다.
                context.Car.Stop();
                context.Aligning = true;
            }

            return context.Car.AlignTo(context.TargetRotation, dt) ? VisitPhase.Unloading : VisitPhase.Arriving;
        }
    }
}
