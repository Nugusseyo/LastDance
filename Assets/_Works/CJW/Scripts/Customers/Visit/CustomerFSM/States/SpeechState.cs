using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DevLib.SoundSystem;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>손님 머리 위에 말풍선을 띄운다. 대사는 HumanDB에서 (손님의 HumanType, lineIndices 중 하나)로 찾고, lineIndices가 비어 있으면 말하지 않는다. 기본은 말풍선이 끝날 때까지 기다리고, runAlongside를 켜면 띄우자마자 다음 행동으로 넘어가 행동과 동시에 대사가 나온다.
    /// 말풍선의 수명은 SpeechBubble이 쥐고 스스로 풀에 돌아가므로 이 상태는 Push하지 않는다.</summary>
    [Serializable]
    public sealed class SpeechState : CustomerState, ISpeakingState
    {
        public int[] SpokenLines => lineIndices;

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
            // 대사가 비었으면 말하지 않는 손님이다. 상태는 모든 손님에 달아 두고 대사만 비워 구분한다.
            // 따라다니기는 runAlongside면 다음 상태로 넘어가도 끊기지 않게 이 상태의 토큰을 주지 않는다.
            if (!CustomerSpeech.TrySay(Ctx, poolManager, bubbleItem, lineIndices, offset, speechSound,
                    runAlongside ? CancellationToken.None : ct, out UniTask following))
            {
                return VisitOutcome.Done;
            }

            if (runAlongside)
            {
                following.Forget();
                return VisitOutcome.Done;
            }

            // 인터럽트나 Phase 전환이면 여기서 취소로 빠져나간다. 말풍선은 따라다니기가 접는다.
            await following;
            return VisitOutcome.Done;
        }
    }
}
