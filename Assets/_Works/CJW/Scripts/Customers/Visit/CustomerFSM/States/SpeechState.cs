using System.Threading;
using _Works.JYG._Scripts.UI.SpeechBubble;
using Cysharp.Threading.Tasks;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    public class SpeechState : CustomerState
    {
        [SerializeReference] private PoolManagerSO poolManager;
        [SerializeReference] private PoolItemSO bubbleSo;
        public override UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            var speechBubble = poolManager.Pop<SpeechBubble>(bubbleSo);
            speechBubble.InitializeBubble(Ctx.Customer.HumanType);

            // return UniTask.;

            // todo speechBubble에서 끝나는 이벤트 받고 UniTask를 끝내게 해야함.
            return default(UniTask<VisitOutcome>);
        }
    }
}   