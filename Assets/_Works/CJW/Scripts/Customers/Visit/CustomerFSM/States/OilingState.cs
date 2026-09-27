using System;
using System.Threading;
using _Works.CJW.Scripts.MapSystems;
using _Works.JJH._02_Scripts.Objects;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    [Serializable]
    public sealed class OilingState : CustomerState, IDestinationState
    {
        public MapPointType Destination => MapPointType.OilDispenser;

        public override bool WantsFuel => true;

        [SerializeField] private float timeout;

        [Tooltip("주유기에 도착한 뒤 플레이어의 주유가 끝나길 기다리는 최대 시간(초). 0 이하면 끝날 때까지 기다린다.")]
        [SerializeField] private float fuelTimeout;

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

                // 주유기에 닿지 못했으면 주유를 기다릴 이유가 없다. 이동 결과를 그대로 넘긴다.
                if (outcome != VisitOutcome.Done)
                {
                    return outcome;
                }

                FuelDoor fuelDoor = Ctx.Visit?.Car != null ? Ctx.Visit.Car.GetComponentInChildren<FuelDoor>(true) : null;

                if (fuelDoor == null)
                {
                    // 여기서 멈추면 방문 전체가 굳는다. 주유를 못 받은 사실만 남기고 넘어간다.
                    Debug.LogError($"[OilingState] {customer.name}의 차에 {nameof(FuelDoor)}가 없어 주유를 기다릴 수 없습니다.", customer);
                    return VisitOutcome.Blocked;
                }

                return await WaitForFuelingEnded(fuelDoor, ct);
            }
            finally
            {
                // Phase 전환이나 인터럽트로 취소되어도 여기는 반드시 지난다.
                // 빼먹으면 그 주유기가 영영 점유 상태로 남아 아무도 쓰지 못한다.
                ReleaseRented();
            }
        }

        /// <summary>플레이어가 이 차의 주유를 마칠 때까지 기다린다. <see cref="FuelDoor.OnFuelingEnded"/>가 유일한 끝 신호다.
        private async UniTask<VisitOutcome> WaitForFuelingEnded(FuelDoor fuelDoor, CancellationToken ct)
        {
            var ended = new UniTaskCompletionSource();
            void OnEnded() => ended.TrySetResult();

            fuelDoor.OnFuelingEnded += OnEnded;

            try
            {
                if (fuelTimeout <= 0f)
                {
                    await ended.Task.AttachExternalCancellation(ct);
                    return VisitOutcome.Done;
                }

                int winner = await UniTask.WhenAny(
                    ended.Task.AttachExternalCancellation(ct),
                    UniTask.Delay(TimeSpan.FromSeconds(fuelTimeout), cancellationToken: ct));

                return winner == 0 ? VisitOutcome.Done : VisitOutcome.Timeout;
            }
            finally
            {
                // 풀링으로 차가 재사용되므로 구독을 남기면 다음 방문의 주유 끝 신호가 이미 끝난 손님에게 간다.
                fuelDoor.OnFuelingEnded -= OnEnded;
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
