using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>걸음걸이 변조를 켜고 끈다. 기다리지 않고 곧바로 끝나므로 시퀀스 중간에 끼워 쓰면 된다.
    /// 프리팹에서 항상 이상하게 걷는 손님은 이 상태가 필요 없다 — 걸음걸이 모듈을 켜 둔 채로 두면 된다.
    /// 술이 깨거나 다쳐서 도중에 걸음이 달라지는 손님만 이걸 쓴다.</summary>
    [Serializable]
    public sealed class SetGaitState : CustomerState
    {
        [Tooltip("켤지 끌지. 끄면 프리팹의 기본 걷기로 돌아간다.")]
        [SerializeField] private bool active = true;

        public override UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            if (Ctx.Customer.Gait == null)
            {
                Debug.LogWarning(
                    $"[{nameof(SetGaitState)}] {Ctx.Customer.name}에 걸음걸이 모듈이 없어 아무것도 바꾸지 못했습니다. " +
                    "프리팹에 OddGaitModule을 붙여야 합니다.", Ctx.Customer);

                return UniTask.FromResult(VisitOutcome.Blocked);
            }

            Ctx.Customer.Gait.SetActive(active);

            return UniTask.FromResult(VisitOutcome.Done);
        }
    }
}
