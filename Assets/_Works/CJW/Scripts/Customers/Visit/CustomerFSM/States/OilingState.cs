using System;
using System.Threading;
using _Works.CJW.Scripts.MapSystems;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    public class OilingState : CustomerState
    {
        [SerializeField] private float timeout;
        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            MapPosition mapPos;
            Vector3 originPos = Ctx.Customer.transform.position;
            
            if (Ctx.MapData.TryRentNearest(MapPointType.OilDispenser, originPos, out var point))
            {
                mapPos = point;
                point.SetOccupied(true);
            }
            else if (!Ctx.MapData.TryGetNearest(MapPointType.WaitingLine, originPos, out mapPos))
            {
                Debug.Log("대기열도 없음.");
                return VisitOutcome.Blocked;
            }
            
            VisitOutcome outcome = await MoveAndWait(mapPos.Position, timeout, ct);
            
            //TODO 주유 기능 추가
            
            if (mapPos is RentableMapPosition rentable)
                rentable.SetOccupied(false);
            return outcome;
        }
    }
}