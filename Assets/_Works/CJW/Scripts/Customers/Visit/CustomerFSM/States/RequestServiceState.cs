using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Customers.Interaction;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>플레이어에게 무언가를 요구하고 답을 기다린다. 바퀴 교체든 네고든 요구의 종류만 다르고 흐름은 같다.
    /// 무엇이 그 요구를 들어주는지는 이 상태가 모른다 — <see cref="ICustomerRequest"/>에 답이 들어오기만 기다린다.
    /// 그래서 요구를 처리하는 시스템이 아직 없어도 손님은 기다리다 지쳐 제 갈 길을 가고, 방문은 굳지 않는다.</summary>
    [Serializable]
    public sealed class RequestServiceState : CustomerState
    {
        [Header("요구")]
        [Tooltip("무엇을 요구하는지.")]
        [SerializeField] private CustomerRequestType requestType = CustomerRequestType.TireChange;

        [Tooltip("요구에 딸린 수치. 네고라면 깎아 달라는 금액처럼 종류마다 뜻이 다르다.")]
        [SerializeField, Min(0f)] private float amount;

        [Header("자리")]
        [Tooltip("요구하기 전에 갈 지점의 종류. None이면 지금 선 자리에서 요구한다.")]
        [SerializeField] private MapPointType requestAt = MapPointType.Garage;

        [Tooltip("그 지점까지 걸어갈 때의 한계 시간(초).")]
        [SerializeField, Min(0f)] private float moveTimeout = 15f;

        [Tooltip("걸어서 닿는 지점이 없을 때 다시 찾아볼 시간(초). 줄지어 선 차가 길을 막았다가 떠나면 열린다.")]
        [SerializeField, Min(0f)] private float reachWait = 5f;

        [Tooltip("켜면 요구하는 동안 자기 차를 돌아본다. 바퀴를 갈아 달라는 손님이 차를 등지고 서 있지 않게 한다.")]
        [SerializeField] private bool faceCar = true;

        [Tooltip("몸을 돌리는 각속도(도/초).")]
        [SerializeField, Min(1f)] private float turnSpeed = 360f;

        [Header("대기")]
        [Tooltip("답을 기다릴 시간(초). 넘기면 요구를 거두고 다음 행동으로 넘어간다. 0이면 Phase가 바뀔 때까지 기다린다.")]
        [SerializeField, Min(0f)] private float waitTimeout = 30f;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;
            ICustomerRequest request = customer.Request;

            if (request == null)
            {
                // 여기서 멈추면 방문 전체가 굳는다. 요구하지 못한 사실만 남기고 넘어간다.
                Debug.LogError(
                    $"[{nameof(RequestServiceState)}] {customer.name}에 요구 모듈이 없어 아무것도 요구하지 못했습니다. " +
                    $"프리팹에 {nameof(CustomerRequestModule)}을 붙여야 합니다.", customer);

                return VisitOutcome.Blocked;
            }

            if (requestAt != MapPointType.None)
            {
                if (Ctx.MapData == null)
                {
                    Debug.LogError($"[{nameof(RequestServiceState)}] CustomerFSMModule에 MapData를 지정해야 합니다.", customer);
                    return VisitOutcome.Failed;
                }

                MapPosition point = await WaitForReachablePoint(requestAt, reachWait, ct);
                if (point != null)
                {
                    VisitOutcome moved = await MoveAndWait(point.Position, moveTimeout, ct);

                    if (moved == VisitOutcome.Blocked)
                    {
                        // 자리에 못 갔다고 요구까지 접지는 않는다. 선 자리에서 요구한다.
                        Debug.LogWarning($"[{nameof(RequestServiceState)}] {customer.name}이(가) {requestAt}에 닿지 못해 제자리에서 요구합니다.", customer);
                    }
                }
                else
                {
                    Debug.LogWarning($"[{nameof(RequestServiceState)}] 맵에 {requestAt} 지점이 없어 제자리에서 요구합니다.", customer);
                }
            }

            customer.Mover?.Stop();

            if (faceCar && Ctx.Visit?.Car != null)
            {
                await FaceTowards(Ctx.Visit.Car.transform.position, turnSpeed, ct);
            }

            request.Raise(requestType, amount);

            try
            {
                float deadline = waitTimeout > 0f ? Time.time + waitTimeout : float.MaxValue;

                while (request.IsPending)
                {
                    if (Time.time > deadline)
                    {
                        // 기다리다 지쳤다. 요구를 거두는 건 finally가 한다.
                        return VisitOutcome.Timeout;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                return request.Result switch
                {
                    CustomerRequestResult.Accepted => VisitOutcome.Done,

                    // 거절도 정상적인 답이다. 방문을 끊지 않고 다음 행동(화내기·퇴장)으로 넘긴다.
                    CustomerRequestResult.Rejected => VisitOutcome.Blocked,
                    _ => VisitOutcome.Timeout
                };
            }
            finally
            {
                // 퇴치나 Phase 전환으로 끊겨도 여기는 반드시 지난다.
                // 빼먹으면 답을 기다리는 요구가 영영 열린 채로 남아 UI가 사라진 손님을 계속 가리킨다.
                Withdraw(true);
            }
        }

        /// <summary>방문이 중단돼 Run이 다시 돌지 않는 경우에도 요구를 닫기 위해 여기서도 정리한다.</summary>
        public override void Reset()
        {
            Withdraw(false);
        }

        private void Withdraw(bool expired)
        {
            ICustomerRequest request = Ctx?.Customer != null ? Ctx.Customer.Request : null;

            if (request != null && request.IsPending)
            {
                request.Withdraw(expired);
            }
        }
    }
}
