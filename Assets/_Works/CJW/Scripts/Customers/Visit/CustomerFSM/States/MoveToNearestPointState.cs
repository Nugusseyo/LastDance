using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>
    /// 정해진 종류의 지점 중 가장 가까운 곳으로 걸어가 도착할 때까지 기다린다.
    /// 씬의 Transform을 직접 참조하지 않으므로 풀링해도 참조가 끊기지 않는다.
    /// </summary>
    [Serializable]
    public sealed class MoveToNearestPointState : CustomerState
    {
        [Tooltip("이 종류의 지점 중 가장 가까운 곳으로 간다.")]
        [SerializeField] private MapPointType targetPoint = MapPointType.ShopEntrance;

        [Tooltip("이 시간 안에 도착하지 못하면 Timeout으로 끝낸다. 0이면 무제한.")]
        [SerializeField, Min(0f)] private float timeout = 15f;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;

            if (Ctx.MapData == null)
            {
                Debug.LogError("[MoveToNearestPoint] CustomerFSMModule에 MapData를 지정해야 합니다.", customer);
                return VisitOutcome.Failed;
            }

            if (!Ctx.MapData.TryGetNearest(targetPoint, customer.transform.position, out MapPosition point))
            {
                Debug.LogWarning($"[MoveToNearestPoint] {targetPoint} 지점을 찾지 못해 이동하지 않습니다.", customer);
                return VisitOutcome.Blocked;
            }

            return await MoveAndWait(point.Position, timeout, ct);
        }
    }
}
