using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using DevLib.SoundSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>차고 앞에 세워 둔 차(<see cref="StealableCar"/>)를 훔쳐 타고 달아난다. 타고 온 차는 자리에 버려진다.
    /// 훔치러 나선 순간부터 방문은 출발 요청을 받지 않고, 훔친 차가 맵을 빠져나가면 방문이 닫힌다.
    /// 훔칠 차가 없거나 닿지 못하면 아무것도 하지 않고 다음 행동으로 넘어가 평소처럼 자기 차로 떠난다.</summary>
    [Serializable]
    public sealed class StealCarState : CustomerState
    {
        [Tooltip("훔칠 차까지 걸어갈 때의 한계 시간(초). 차고가 멀어 넉넉하게 둔다.")]
        [SerializeField, Min(0f)] private float walkTimeout = 60f;

        [Tooltip("훔친 차가 퇴장 지점까지 달리는 한계 시간(초). 넘기면 그 자리에서 차를 치우고 방문을 닫는다.")]
        [SerializeField, Min(0f)] private float driveTimeout = 40f;

        [Tooltip("훔칠 차에 닿는 길이 없을 때 다시 찾아볼 시간(초). 줄지어 선 차가 길을 막았다가 떠나면 열린다.")]
        [SerializeField, Min(0f)] private float reachWait = 5f;

        [Header("사운드")]
        [Tooltip("훔친 차에 올라탈 때 낼 소리(문을 거칠게 여닫는 소리). 시동·급출발 소리는 StealableCar가 낸다.")]
        [SerializeField] private SoundClipSo boardSound;

        /// <summary>경로 계산용 공용 버퍼. 필드 초기화로 만들면 직렬화 도중에 생성돼 Unity가 막으므로, 처음 쓸 때 만든다.</summary>
        private static NavMeshPath _path;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;
            VisitSession session = customer.Session;
            if (session == null || customer.Boarding == null)
            {
                return VisitOutcome.Blocked;
            }

            StealableCar target = await WaitForReachableCar(ct);
            if (target == null || !target.TryClaim())
            {
                Debug.LogWarning($"[{nameof(StealCarState)}] {customer.name}이(가) 훔칠 차를 찾지 못해 평소처럼 떠납니다.", customer);
                return VisitOutcome.Blocked;
            }

            session.BeginAbandon(target);
            bool boarded = false;

            try
            {
                // 제시간에 닿지 못했으면 태우지 않는다. 멀리서 좌석으로 순간이동하는 그림이 된다.
                VisitOutcome walked = await MoveAndWait(target.DoorPosition, walkTimeout, ct);
                if (walked != VisitOutcome.Done)
                {
                    Debug.LogWarning($"[{nameof(StealCarState)}] {customer.name}이(가) {target.name}까지 가지 못해({walked}) 훔치기를 포기합니다.", customer);
                    return GiveUp(session, target);
                }

                customer.Boarding.Board(target.DriverSeat);
                customer.Sound?.Play(boardSound, target.DoorPosition);
                boarded = true;
                target.DriveAway();

                float deadline = driveTimeout > 0f ? Time.time + driveTimeout : float.MaxValue;
                while (!target.HasEscaped && Time.time < deadline)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                if (!target.HasEscaped)
                {
                    Debug.LogWarning($"[{nameof(StealCarState)}] {target.name}이(가) 제시간에 빠져나가지 못해 그 자리에서 치웁니다.", target);
                }

                session.FinishAbandon();
                return VisitOutcome.Done;
            }
            catch (OperationCanceledException)
            {
                // 걸어가다 끊겼으면 찜을 풀어 다른 손님이 노릴 수 있게 한다. 이미 타고 달리는 중이면 되돌릴 수 없으니 방문을 닫는다.
                if (boarded)
                {
                    session.FinishAbandon();
                }
                else
                {
                    GiveUp(session, target);
                }

                throw;
            }
        }

        private static VisitOutcome GiveUp(VisitSession session, StealableCar target)
        {
            target.ReleaseClaim();
            session.CancelAbandon();
            return VisitOutcome.Blocked;
        }

        /// <summary>닿는 차가 나올 때까지 <see cref="reachWait"/>초 동안 다시 찾아본다.</summary>
        private async UniTask<StealableCar> WaitForReachableCar(CancellationToken ct)
        {
            const float retryInterval = 0.5f;
            float deadline = Time.time + reachWait;

            while (true)
            {
                StealableCar car = FindReachableCar();
                if (car != null || Time.time >= deadline)
                {
                    return car;
                }

                await UniTask.Delay(TimeSpan.FromSeconds(retryInterval), cancellationToken: ct);
            }
        }

        /// <summary>아직 아무도 찜하지 않은 차 중 걸어서 끝까지 닿는 가장 가까운 차(경로 길이 기준).</summary>
        private StealableCar FindReachableCar()
        {
            NavMeshAgent agent = Ctx.Customer.Agent;
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                return null;
            }

            _path ??= new NavMeshPath();
            IReadOnlyList<StealableCar> cars = StealableCar.All;
            StealableCar best = null;
            float bestLength = float.MaxValue;

            for (int i = 0; i < cars.Count; i++)
            {
                StealableCar car = cars[i];
                if (car == null || car.IsClaimed)
                {
                    continue;
                }

                if (!agent.CalculatePath(car.DoorPosition, _path) || _path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                float length = 0f;
                Vector3[] corners = _path.corners;
                for (int c = 1; c < corners.Length; c++)
                {
                    length += Vector3.Distance(corners[c - 1], corners[c]);
                }

                if (length < bestLength)
                {
                    bestLength = length;
                    best = car;
                }
            }

            return best;
        }
    }
}
