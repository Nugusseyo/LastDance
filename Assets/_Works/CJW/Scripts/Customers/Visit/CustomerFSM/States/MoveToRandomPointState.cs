using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>정해진 종류의 지점 중 아무 데나 골라 걸어간다. 가장 가까운 곳으로 가는
    /// <see cref="MoveToNearestPointState"/>와 달리 손님마다 다른 곳으로 흩어지므로, 한 자리에 뭉치지 않는다.
    /// 자리를 빌리지는 않는다 — 한 명만 쓸 수 있는 지점에는 <see cref="OilingState"/>처럼 빌리는 상태를 써야 한다.</summary>
    [Serializable]
    public sealed class MoveToRandomPointState : CustomerState
    {
        [Tooltip("이 종류의 지점 중 하나를 무작위로 고른다.")]
        [SerializeField] private MapPointType targetPoint = MapPointType.Table;

        [Tooltip("이 시간 안에 도착하지 못하면 Timeout으로 끝낸다. 0이면 무제한.")]
        [SerializeField, Min(0f)] private float timeout = 15f;

        [Tooltip("켜면 그 종류의 지점이 하나도 없을 때 가게 안 위치로라도 간다. 끄면 제자리에 선다.")]
        [SerializeField] private bool fallbackToShopPoint = true;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;

            if (Ctx.MapData == null)
            {
                Debug.LogError($"[{nameof(MoveToRandomPointState)}] CustomerFSMModule에 MapData를 지정해야 합니다.", customer);
                return VisitOutcome.Failed;
            }

            if (Ctx.MapData.TryGetRandom(targetPoint, out MapPosition point))
            {
                return await MoveAndWait(point.Position, timeout, ct);
            }

            if (fallbackToShopPoint && Ctx.Visit != null)
            {
                // 갈 곳이 없다고 손님을 차 옆에 세워 두면 다음 손님이 내릴 자리를 막는다. 가게 쪽으로라도 보낸다.
                Debug.LogWarning($"[{nameof(MoveToRandomPointState)}] {targetPoint} 지점이 없어 가게 위치로 갑니다.", customer);
                return await MoveAndWait(Ctx.Visit.ShopPoint, timeout, ct);
            }

            Debug.LogWarning($"[{nameof(MoveToRandomPointState)}] {targetPoint} 지점을 찾지 못해 이동하지 않습니다.", customer);
            return VisitOutcome.Blocked;
        }
    }
}
