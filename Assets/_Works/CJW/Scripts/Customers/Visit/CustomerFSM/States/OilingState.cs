using System;
using System.Threading;
using _Works.CJW.Scripts.MapSystems;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    [Serializable]
    public sealed class OilingState : CustomerState
    {
        [SerializeField] private float timeout;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;

            if (Ctx.MapData == null)
            {
                Debug.LogError("[OilingState] CustomerFSMModule에 MapData를 지정해야 합니다.", customer);
                return VisitOutcome.Failed;
            }

            Vector3 originPos = customer.transform.position;
            MapPosition mapPos;

            if (Ctx.MapData.TryRentNearest(MapPointType.OilDispenser, originPos, out RentableMapPosition rented))
            {
                // TryRentNearest가 이미 점유 처리까지 끝냈다. 여기서 다시 걸지 않는다.
                // 빌린 사실을 컨텍스트에 남겨야 취소·반납 경로에서도 짝을 맞출 수 있다.
                Ctx.RentedPosition = rented;
                mapPos = rented;
            }
            else if (!Ctx.MapData.TryGetNearest(MapPointType.WaitingLine, originPos, out mapPos))
            {
                Debug.Log("대기열도 없음.");
                return VisitOutcome.Blocked;
            }

            try
            {
                VisitOutcome outcome = await MoveAndWait(mapPos.Position, timeout, ct);

                //TODO 주유 기능 추가

                return outcome;
            }
            finally
            {
                // Phase 전환이나 인터럽트로 취소되어도 여기는 반드시 지난다.
                // 빼먹으면 그 주유기가 영영 점유 상태로 남아 아무도 쓰지 못한다.
                ReleaseRented();
            }
        }

        /// <summary>방문이 중단돼 Run이 다시 돌지 않는 경우에도 자리를 돌려주기 위해 여기서도 정리한다.</summary>
        public override void Reset()
        {
            ReleaseRented();
        }

        private void ReleaseRented()
        {
            if (Ctx?.RentedPosition == null)
            {
                return;
            }

            // SetOccupied를 직접 부르지 않는다. MapData를 거쳐야 Changed가 날아가 인스펙터·UI가 따라온다.
            Ctx.MapData?.Release(Ctx.RentedPosition);
            Ctx.RentedPosition = null;
        }
    }
}
