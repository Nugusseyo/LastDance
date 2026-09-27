using System;
using System.Threading;
using _Works.JYG._Scripts.UI.SpeechBubble;
using Cysharp.Threading.Tasks;
using DevLib.SoundSystem;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>손님 머리 위에 말풍선을 띄운다. 대사는 HumanDB에서 (손님의 HumanType, lineIndices 중 하나)로 찾고, lineIndices가 비어 있으면 말하지 않는다. 기본은 말풍선이 끝날 때까지 기다리고, runAlongside를 켜면 띄우자마자 다음 행동으로 넘어가 행동과 동시에 대사가 나온다.
    /// 말풍선의 수명은 SpeechBubble이 쥐고 스스로 풀에 돌아가므로 이 상태는 Push하지 않는다.</summary>
    [Serializable]
    public sealed class SpeechState : CustomerState
    {
        [Header("풀")]
        [Tooltip("말풍선을 꺼내 올 풀. 씬 어딘가에서 InitializePool이 먼저 불려 있어야 한다.")]
        [SerializeField] private PoolManagerSO poolManager;

        [Tooltip("말풍선 프리팹의 풀 아이템.")]
        [SerializeField] private PoolItemSO bubbleItem;

        [Header("대사")]
        [Tooltip("HumanDB의 대사 index 후보. 비우면 이 손님은 말하지 않는다.\n" +
                 "여럿이면 방문마다 하나를 고른다(CustomerContext.PickVariant). 춤처럼 같은 갈래를 쓰는 상태와 짝이 맞고, 싸우는 짝과는 서로 다른 대사가 된다.")]
        [SerializeField] private int[] lineIndices;

        [Header("표시")]
        [Tooltip("손님 기준으로 말풍선을 띄울 위치(m). 머리 위로 올리려면 y를 키운다.")]
        [SerializeField] private Vector3 offset = new(0f, 2.2f, 0f);

        [Tooltip("켜면 말풍선을 띄우자마자 다음 상태로 넘어간다. 말풍선은 대사가 끝날 때까지 손님을 따라다닌다.\n" +
                 "끄면(기본) 대사가 끝날 때까지 이 상태에 머문다. 기본값을 false로 둔 건 이 필드가 없던 프리팹이 기존처럼 기다리게 하기 위해서다.")]
        [SerializeField] private bool runAlongside;

        [Header("사운드")]
        [Tooltip("말풍선이 뜰 때 낼 소리(웅얼거리는 말소리).")]
        [SerializeField] private SoundClipSo speechSound;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            if (lineIndices == null || lineIndices.Length == 0)
            {
                // 말하지 않는 손님. 상태는 모든 손님에 달아 두고 대사만 비워 구분한다.
                return VisitOutcome.Done;
            }

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

            Transform anchor = Ctx.Customer.transform;
            Follow(bubble, anchor);

            // 앞 대사가 아직 떠 있으면 접는다. 한 손님 머리 위에 말풍선이 겹쳐 뜨지 않게 한다.
            Ctx.EndSpeech();

            // 컨텍스트에 맡겨 두면 주유를 받거나 죽었을 때 대사가 끝나길 기다리지 않고 바로 접힌다.
            Action end = bubble.EndSpeech;
            Ctx.SetSpeech(end);

            // 구독은 InitializeBubble보다 먼저 건다. 대사가 없으면 그 안에서 바로 OnSpeechEnd가 불린다.
            UniTask following = FollowUntilEnd(bubble, anchor, end, runAlongside ? CancellationToken.None : ct);

            int lineIndex = lineIndices[Ctx.PickVariant(lineIndices.Length)];
            Ctx.LineIndex = lineIndex;
            bubble.InitializeBubble(Ctx.Customer.HumanType, lineIndex);
            Ctx.Customer.Sound?.Play(speechSound);
            Debug.Log($"[SpeechState] {Ctx.Customer.name} 말풍선 시작 (대사 {lineIndex})", Ctx.Customer);

            if (runAlongside)
            {
                // 이 상태의 토큰은 다음 상태로 넘어가면 취소되므로 따라다니기는 말풍선이 끝날 때까지 따로 돈다.
                following.Forget();
                return VisitOutcome.Done;
            }

            // 인터럽트나 Phase 전환이면 여기서 취소로 빠져나간다.
            // 말풍선은 남은 시간을 마저 세고 스스로 풀로 돌아가므로 따로 치우지 않는다.
            await following;
            return VisitOutcome.Done;
        }

        /// <summary>말풍선이 끝날 때까지 손님 머리 위를 따라다니게 한다.</summary>
        private async UniTask FollowUntilEnd(SpeechBubble bubble, Transform anchor, Action end, CancellationToken ct)
        {
            CustomerContext ctx = Ctx;

            // 이벤트는 await할 수 없으므로 플래그로 바꿔 문다.
            // 맡긴 말풍선은 끝나는 그 순간 거둔다. 풀로 돌아간 말풍선이 다른 손님에게 다시 나간 뒤 이 손님 쪽에서 접히면 안 된다.
            bool ended = false;
            void OnSpeechEnd()
            {
                ended = true;
                ctx.ClearSpeech(end);
            }

            bubble.OnSpeechEnd += OnSpeechEnd;

            try
            {
                while (!ended)
                {
                    // 플레이 종료처럼 말풍선이나 손님이 먼저 파괴되거나, 손님이 풀로 돌아가 꺼지면 더 따라다닐 대상이 없다.
                    if (bubble == null || anchor == null || !anchor.gameObject.activeInHierarchy)
                    {
                        return;
                    }

                    Follow(bubble, anchor);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }
            finally
            {
                // 취소로 빠져나가도 풀에 돌아간 말풍선이 이 손님의 핸들러를 계속 들고 있지 않게 한다.
                if (bubble != null)
                {
                    bubble.OnSpeechEnd -= OnSpeechEnd;
                }

                ctx.ClearSpeech(end);
            }
        }

        /// <summary>손님 머리 위에 두고 카메라와 같은 방향을 보게 한다.
        /// 월드 캔버스는 얇은 판이라 회전을 두지 않으면 차 옆(주유구 쪽)에서 볼 때 옆면만 보여 사라진 것처럼 된다.</summary>
        private void Follow(SpeechBubble bubble, Transform anchor)
        {
            bubble.transform.position = anchor.position + offset;

            Camera cam = Camera.main;
            if (cam != null)
            {
                // 판의 앞(+Z)을 카메라 앞과 맞춰야 글자가 뒤집히지 않는다.
                bubble.transform.rotation = cam.transform.rotation;
            }
        }
    }
}
