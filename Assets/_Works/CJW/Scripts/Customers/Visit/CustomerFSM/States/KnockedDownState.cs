using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>쓰러져 있는 동안 아무것도 하지 않는다. 인터럽트로만 들어오고, 몸이 일어서면 끝나 원래 하던 행동으로 돌아간다.
    /// 프리팹에 직렬화하지 않는다 — 쓰러짐은 어느 손님이든 같으니 FSM 모듈이 코드로 하나 만들어 쓴다.</summary>
    [Serializable]
    public sealed class KnockedDownState : CustomerState
    {
        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            await UniTask.WaitWhile(() => Ctx.Customer != null && Ctx.Customer.IsKnockedDown, cancellationToken: ct);
            return VisitOutcome.Done;
        }
    }
}
