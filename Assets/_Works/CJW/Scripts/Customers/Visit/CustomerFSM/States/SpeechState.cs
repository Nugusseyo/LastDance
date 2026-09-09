using System;
using System.Threading;
using _Works.JYG._Scripts.UI.SpeechBubble;
using Cysharp.Threading.Tasks;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>
    /// 손님 머리 위에 말풍선을 띄우고, 말풍선이 스스로 끝날 때까지 기다린다.
    ///
    /// 말풍선의 수명은 SpeechBubble이 쥔다. InitializeBubble이 코루틴을 걸고,
    /// 시간이 다 되면 OnSpeechEnd를 쏜 뒤 스스로 풀에 돌아간다.
    /// 그래서 이 상태는 Push하지 않는다 — 두 번 넣으면 같은 말풍선이 풀에 둘로 쌓여
    /// 서로 다른 손님이 같은 오브젝트를 동시에 쓰게 된다.
    /// 예외는 하나, 말풍선이 코루틴을 시작하지도 못한 경우다. 그때만 이쪽이 치운다.
    /// </summary>
    [Serializable]
    public sealed class SpeechState : CustomerState
    {
        [Header("풀")]
        [Tooltip("말풍선을 꺼내 올 풀. 씬 어딘가에서 InitializePool이 먼저 불려 있어야 한다.")]
        [SerializeField] private PoolManagerSO poolManager;

        [Tooltip("말풍선 프리팹의 풀 아이템.")]
        [SerializeField] private PoolItemSO bubbleItem;

        [Header("표시")]
        [Tooltip("손님 기준으로 말풍선을 띄울 위치(m). 머리 위로 올리려면 y를 키운다.")]
        [SerializeField] private Vector3 offset = new(0f, 2.2f, 0f);

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            if (poolManager == null || bubbleItem == null)
            {
                Debug.LogWarning("[SpeechState] 풀 또는 말풍선 아이템이 비어 있어 말풍선을 건너뜁니다.", Ctx.Customer);
                return VisitOutcome.Done;
            }

            SpeechBubble bubble = poolManager.Pop<SpeechBubble>(bubbleItem);

            if (bubble == null)
            {
                Debug.LogWarning($"[SpeechState] {bubbleItem.name}이(가) 풀에 등록되어 있지 않습니다.", Ctx.Customer);
                return VisitOutcome.Done;
            }

            // 이벤트는 await할 수 없으므로 플래그로 바꿔 문다.
            bool ended = false;
            void OnSpeechEnd() => ended = true;

            bubble.OnSpeechEnd += OnSpeechEnd;

            Transform anchor = Ctx.Customer.transform;
            bubble.transform.position = anchor.position + offset;

            bubble.InitializeBubble(Ctx.Customer.HumanType);
            Debug.Log("[SpeechState] 말풍선 시작", Ctx.Customer);

            while (!ended)
            {
                bubble.transform.position = anchor.position + offset;

                // 인터럽트나 Phase 전환이면 여기서 취소로 빠져나간다.
                // 말풍선은 남은 시간을 마저 세고 스스로 풀로 돌아가므로 따로 치우지 않는다.
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            bubble.OnSpeechEnd -= OnSpeechEnd;
            return VisitOutcome.Done;
        }
    }
}
