using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Movement;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>자기 차 둘레의 아무 데나 걸어가 잠깐 서 있기를 되풀이한다. 스스로 끝나지 않고 방문 단계가 바뀔 때(탑승)까지 돈다.
    /// 진상 짓을 할 조건이 안 맞은 손님(싸울 짝·때릴 차가 없음)이 멍하니 서 있지 않고 일반 손님처럼 보이게 한다 —
    /// 그래서 시작하면 <see cref="CustomerContext.Harmless"/>를 켜 평판도 일반인으로 셈하게 한다.
    /// <see cref="MeetUpState"/>·<see cref="VandalizeState"/>가 조건이 안 맞을 때 코드로 만들어 돌리고, 시퀀스에 직접 넣어도 된다.</summary>
    [Serializable]
    public sealed class WanderState : CustomerState
    {
        [Tooltip("자기 차에서 이 반경(m) 안에서 고른다. 지금 자리를 중심으로 고르면 걸음마다 조금씩 멀어져 결국 가게를 벗어난다.")]
        [SerializeField, Min(2f)] private float radius = 12f;

        [Tooltip("자기 차에서 최소 이만큼(m)은 떨어진 곳을 고른다. 차 옆에 서면 동행이 타고 내리는 길을 막는다.")]
        [SerializeField, Min(0f)] private float minDistanceFromCar = 4f;

        [Tooltip("한 번 걸어가는 데 쓰는 최대 시간(초). 넘기면 그 자리에서 멈추고 다음 자리를 고른다.")]
        [SerializeField, Min(1f)] private float walkTimeout = 12f;

        [Tooltip("도착해서 서 있는 시간(초)의 최솟값.")]
        [SerializeField, Min(0f)] private float pauseMin = 2f;

        [Tooltip("도착해서 서 있는 시간(초)의 최댓값.")]
        [SerializeField, Min(0f)] private float pauseMax = 5f;

        /// <summary>한 번에 뽑아 볼 자리 수.</summary>
        private const int Tries = 20;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            Ctx.Harmless = true;

            AbstractCustomer customer = Ctx.Customer;
            IMover mover = customer.Mover;
            customer.ActionAnimator?.End();

            while (true)
            {
                if (mover != null && mover.IsReady && TryPickSpot(out Vector3 destination))
                {
                    await mover.MoveAndWait(destination, walkTimeout, ct);
                    mover.Stop();
                }

                float pause = Random.Range(pauseMin, Mathf.Max(pauseMin, pauseMax));
                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0.5f, pause)), cancellationToken: ct);
            }
        }

        private bool TryPickSpot(out Vector3 destination)
        {
            destination = default;
            NavMeshAgent agent = Ctx.Customer.Agent;
            if (Ctx.MapData == null || agent == null)
            {
                return false;
            }

            Car ownCar = Ctx.Visit?.Car;
            Vector3 center = ownCar != null ? ownCar.transform.position : Ctx.Customer.transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

            return Ctx.MapData.TryGetRandomPosition(center, radius, filter, Tries, IsGoodSpot, out destination);
        }

        private bool IsGoodSpot(Vector3 position)
        {
            Car ownCar = Ctx.Visit?.Car;
            if (ownCar != null && minDistanceFromCar > 0f)
            {
                Vector3 delta = position - ownCar.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistanceFromCar * minDistanceFromCar)
                {
                    return false;
                }
            }

            // 빙 돌아가야 닿는 곳(주유기 섬 건너편 등)은 버린다.
            return WalkLengthTo(position) <= radius * 2f;
        }
    }
}
