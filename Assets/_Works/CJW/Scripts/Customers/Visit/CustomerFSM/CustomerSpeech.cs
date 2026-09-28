using System;
using System.Threading;
using _Works.CJW.Scripts.Customers.Data;
using _Works.JYG._Scripts.UI.SpeechBubble;
using Cysharp.Threading.Tasks;
using DevLib.ObjectPool.Runtime;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>손님 머리 위에 말풍선을 띄우는 공통 절차. <see cref="States.SpeechState"/>처럼 말하기만 하는 상태도, 싸움처럼 하던 행동 도중
    /// 한마디 하는 상태도 이걸 쓴다. 대사는 HumanDB에서 (대사 index의 HumanDB type, 대사 index)로 찾는다 — 프리팹의 HumanType은 index가 HumanDB에 없을 때만 쓴다.
    /// 말풍선의 수명은 SpeechBubble이 쥐고 스스로 풀에 돌아가므로 여기서 Push하지 않는다.</summary>
    public static class CustomerSpeech
    {
        /// <summary>위치를 따로 정하지 않았을 때 말풍선을 띄울 자리(손님 기준, m). 머리 위다.</summary>
        public static readonly Vector3 DefaultOffset = new(0f, 2.2f, 0f);

        /// <summary>후보 중 이번 방문의 갈래(<see cref="CustomerContext.PickVariant"/>)로 대사를 골라 띄운다. 후보가 비었거나 풀이 없으면 띄우지 않는다.
        /// <paramref name="following"/>은 말풍선이 끝날 때까지 머리 위를 따라다니는 작업이다 — 기다리면 대사가 끝날 때까지 멈추고,
        /// Forget하면 행동과 동시에 말한다. <paramref name="followToken"/>이 취소되면 말풍선을 접는다.</summary>
        public static bool TrySay(CustomerContext ctx, PoolManagerSO pool, PoolItemSO bubbleItem, int[] lineIndices,
                                  Vector3 offset, SoundClipSo sound, CancellationToken followToken, out UniTask following)
        {
            following = UniTask.CompletedTask;

            if (ctx?.Customer == null || lineIndices == null || lineIndices.Length == 0)
            {
                return false;
            }

            return TrySayLine(ctx, pool, bubbleItem, lineIndices[ctx.PickVariant(lineIndices.Length)], offset, sound, followToken, out following);
        }

        /// <summary>대사 index를 직접 정해 띄운다. 싸움 중 번갈아 하는 말처럼 방문마다 고정된 갈래가 아니라 매번 다른 대사를 고를 때 쓴다.</summary>
        public static bool TrySayLine(CustomerContext ctx, PoolManagerSO pool, PoolItemSO bubbleItem, int lineIndex,
                                      Vector3 offset, SoundClipSo sound, CancellationToken followToken, out UniTask following)
        {
            following = UniTask.CompletedTask;

            if (ctx?.Customer == null)
            {
                return false;
            }

            // [SerializeReference] 상태에 새로 단 위치 필드는 기존 프리팹에 0으로 들어온다. 발밑에 띄우지 않게 기본 위치로 바꾼다.
            if (offset == Vector3.zero)
            {
                offset = DefaultOffset;
            }

            if (pool == null || bubbleItem == null)
            {
                Debug.LogWarning("[CustomerSpeech] 풀 또는 말풍선 아이템이 비어 있어 말풍선을 건너뜁니다.", ctx.Customer);
                return false;
            }

            SpeechBubble bubble = pool.Pop<SpeechBubble>(bubbleItem);
            if (bubble == null)
            {
                Debug.LogWarning($"[CustomerSpeech] {bubbleItem.name}이(가) 풀에 등록되어 있지 않습니다.", ctx.Customer);
                return false;
            }

            Transform anchor = ctx.Customer.transform;
            Follow(bubble, anchor, offset);

            // 앞 대사가 아직 떠 있으면 접는다. 한 손님 머리 위에 말풍선이 겹쳐 뜨지 않게 한다.
            ctx.EndSpeech();

            // 컨텍스트에 맡겨 두면 주유를 받거나 죽었을 때 대사가 끝나길 기다리지 않고 바로 접힌다.
            Action end = bubble.EndSpeech;
            ctx.SetSpeech(end);

            // 구독은 InitializeBubble보다 먼저 건다. 대사가 없으면 그 안에서 바로 OnSpeechEnd가 불린다.
            following = FollowUntilEnd(ctx, bubble, anchor, offset, end, followToken);

            ctx.LineIndex = lineIndex;
            bubble.InitializeBubble(HumanTypeTable.Of(lineIndex, ctx.Customer.HumanType), lineIndex);
            ctx.Customer.Sound?.Play(sound);
            Debug.Log($"[CustomerSpeech] {ctx.Customer.name} 말풍선 시작 (대사 {lineIndex})", ctx.Customer);
            return true;
        }

        /// <summary>말풍선이 끝날 때까지 손님 머리 위를 따라다니게 한다.</summary>
        private static async UniTask FollowUntilEnd(CustomerContext ctx, SpeechBubble bubble, Transform anchor, Vector3 offset,
                                                    Action end, CancellationToken ct)
        {
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

                    Follow(bubble, anchor, offset);
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

                // 대사가 끝나기 전에 빠져나왔다 — 상태가 끊겼거나(맞음·단계 전환) 손님이 풀로 돌아가 꺼졌다.
                // 여기서 놓아 버리면 말풍선은 주인 없이 그 자리에 멈춰, 제 시간(주유 대사는 최대 240초)을 다 채울 때까지 떠 있다.
                // 끊긴 대사는 상태가 다시 돌 때 새로 띄운다.
                if (!ended && bubble != null && bubble.isActiveAndEnabled)
                {
                    bubble.EndSpeech();
                }
            }
        }

        /// <summary>손님 머리 위에 두고 카메라와 같은 방향을 보게 한다.
        /// 월드 캔버스는 얇은 판이라 회전을 두지 않으면 차 옆(주유구 쪽)에서 볼 때 옆면만 보여 사라진 것처럼 된다.</summary>
        private static void Follow(SpeechBubble bubble, Transform anchor, Vector3 offset)
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
