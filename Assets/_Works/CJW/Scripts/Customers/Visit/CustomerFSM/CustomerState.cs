using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>
    /// 손님 행동 하나. [SerializeReference]로 프리팹에 직접 직렬화되므로
    /// 설정값이 그 손님 타입에만 속한다 — 같은 행동을 손님마다 다른 값으로 쓸 수 있다.
    /// 프리팹을 Instantiate하면 이 객체도 함께 복제되어 손님마다 자기 인스턴스를 갖는다.
    /// 그래서 필드를 가져도 안전하다.
    ///
    /// Enter / Tick / Exit이 Run 하나의 앞·중간·뒤로 접힌다.
    /// 정리 코드가 필요하면 try/finally를 쓰면 취소와 정상 종료가 모두 커버된다.
    ///
    /// 클래스 이름이나 네임스페이스를 바꾸면 직렬화 참조가 끊기므로
    /// 옮길 때는 [MovedFrom] 어트리뷰트를 붙인다.
    /// </summary>
    [Serializable]
    public abstract class CustomerState
    {
        protected CustomerContext Ctx { get; private set; }

        public void Bind(CustomerContext ctx)
        {
            Ctx = ctx;
        }

        /// <summary>
        /// 이 행동을 수행하고 어떻게 끝났는지 반환한다.
        /// 모든 대기에 <paramref name="ct"/>를 물려야 한다. 하나라도 빠지면
        /// 손님을 풀에 반납한 뒤에도 태스크가 계속 돈다.
        /// </summary>
        public abstract UniTask<VisitOutcome> Run(CancellationToken ct);

        /// <summary>
        /// 방문 시작 시 호출. 인스턴스가 재사용되므로 진행값을 들고 있다면 여기서 되돌린다.
        /// </summary>
        public virtual void Reset() { }

        /// <summary>
        /// 목적지로 이동하고 도착할 때까지 기다리는 공용 절차.
        /// 이동을 쓰는 상태가 여럿이라 여기에 둔다.
        /// </summary>
        protected async UniTask<VisitOutcome> MoveAndWait(Vector3 destination, float timeout, CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;

            // 하차 직후에는 탑승 모듈이 Agent를 다시 켜는 데 몇 프레임이 걸릴 수 있다.
            // 곧바로 Blocked로 빠지지 않도록 잠깐 기다려 준다.
            float ready = Time.time + 1f;
            while (customer.Agent == null || !customer.Agent.enabled || !customer.Agent.isOnNavMesh)
            {
                if (Time.time > ready)
                {
                    return VisitOutcome.Blocked;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            customer.MoveTo(destination);

            // SetDestination 직후 같은 프레임에는 pathPending이 아직 false이고
            // remainingDistance가 0이라 IsArrived가 곧바로 true가 된다. 한 프레임 양보한다.
            await UniTask.NextFrame(ct);
            await UniTask.WaitWhile(() => customer.Agent.pathPending, cancellationToken: ct);

            if (!customer.Agent.hasPath)
            {
                return VisitOutcome.Blocked;
            }

            float deadline = timeout > 0f ? Time.time + timeout : float.MaxValue;

            while (!customer.IsArrived)
            {
                if (Time.time > deadline)
                {
                    return VisitOutcome.Timeout;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Debug.Log("<color=red>도착</color>");
            return VisitOutcome.Done;
        }
    }
}
