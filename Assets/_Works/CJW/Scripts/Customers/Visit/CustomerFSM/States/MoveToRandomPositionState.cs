using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>등록된 지점이 아니라 구운 NavMesh 위 아무 데나 골라 걸어간다. 춤추는 손님처럼 매번 다른 자리에서
    /// 소란을 피워야 하는 손님용이다. 정해진 지점 중에서 고르려면 <see cref="MoveToRandomPointState"/>를 쓴다.</summary>
    [Serializable]
    public sealed class MoveToRandomPositionState : CustomerState
    {
        [Tooltip("랜덤 위치를 고를 중심. None이면 지금 선 자리를 중심으로 삼고, 고르면 그 종류 중 가장 가까운 지점을 중심으로 삼는다.")]
        [SerializeField] private MapPointType anchor = MapPointType.None;

        [Tooltip("중심에서 이 반경(m) 안에서만 고른다.")]
        [SerializeField, Min(1f)] private float radius = 15f;

        [Tooltip("걸어가는 길이의 상한(m). 길이 빙 돌아가는 곳은 버린다.")]
        [SerializeField, Min(1f)] private float maxWalkLength = 25f;

        [Tooltip("자기 차에서 최소 이만큼(m)은 떨어진 곳을 고른다. 차 옆에 서 있으면 동행이 내리고 타는 길을 막는다.")]
        [SerializeField, Min(0f)] private float minDistanceFromCar = 4f;

        [Tooltip("랜덤 위치를 몇 번까지 뽑아 볼지. 모두 조건에 안 맞으면 중심으로 간다.")]
        [SerializeField, Min(1)] private int tries = 30;

        [Tooltip("이 시간 안에 도착하지 못하면 Timeout으로 끝낸다. 0이면 무제한.")]
        [SerializeField, Min(0f)] private float timeout = 40f;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;

            if (Ctx.MapData == null)
            {
                Debug.LogError($"[{nameof(MoveToRandomPositionState)}] CustomerFSMModule에 MapData를 지정해야 합니다.", customer);
                return VisitOutcome.Failed;
            }

            NavMeshAgent agent = customer.Agent;
            if (agent == null)
            {
                Debug.LogError($"[{nameof(MoveToRandomPositionState)}] {customer.name}에 NavMeshAgent가 없어 위치를 고르지 못합니다.", customer);
                return VisitOutcome.Blocked;
            }

            Vector3 center = customer.transform.position;
            if (anchor != MapPointType.None)
            {
                if (Ctx.MapData.TryGetNearest(anchor, center, out MapPosition point))
                {
                    center = point.Position;
                }
                else
                {
                    Debug.LogWarning($"[{nameof(MoveToRandomPositionState)}] {anchor} 지점이 없어 지금 자리를 중심으로 고릅니다.", customer);
                }
            }

            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

            _sampled = _nearCar = _tooFar = 0;

            if (Ctx.MapData.TryGetRandomPosition(center, radius, filter, tries, IsGoodSpot, out Vector3 destination))
            {
                return await MoveAndWait(destination, timeout, ct);
            }

            // 조건에 맞는 자리가 없다고 제자리에 서 있으면 차 옆을 막는다. 중심으로라도 간다.
            // 어느 조건에서 걸렸는지 남겨야 반경·길이 상한을 고칠지 맵을 고칠지 알 수 있다.
            Debug.LogWarning($"[{nameof(MoveToRandomPositionState)}] 랜덤 위치를 찾지 못해 중심으로 갑니다. " +
                             $"(NavMesh 위 후보 {_sampled}/{tries}, 차에 너무 가까움 {_nearCar}, 길이 없거나 너무 멂 {_tooFar})", customer);
            return await MoveAndWait(center, timeout, ct);
        }

        private int _sampled;
        private int _nearCar;
        private int _tooFar;

        private bool IsGoodSpot(Vector3 position)
        {
            _sampled++;

            if (minDistanceFromCar > 0f && Ctx.Visit?.Car != null)
            {
                Vector3 delta = position - Ctx.Visit.Car.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistanceFromCar * minDistanceFromCar)
                {
                    _nearCar++;
                    return false;
                }
            }

            if (WalkLengthTo(position) > maxWalkLength)
            {
                _tooFar++;
                return false;
            }

            return true;
        }
    }
}
