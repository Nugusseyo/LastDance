using System;
using System.Threading;
using _Works.CJW.Scripts.Sounds;
using Cysharp.Threading.Tasks;
using DevLib.AnimatorSystem;
using DevLib.SoundSystem;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>제자리에서 연출 클립을 재생한다. 춤이든 화내는 몸짓이든 이 상태 하나로 표현되므로,
    /// 손님 종류가 늘 때마다 상태 클래스를 새로 만들 필요가 없다 — 프리팹에서 클립만 갈아끼우면 된다.</summary>
    [Serializable]
    public sealed class PlayAnimationState : CustomerState
    {
        [Tooltip("재생할 클립 후보. 여럿이면 매번 하나를 무작위로 고른다. 손님마다 다른 춤을 추게 하는 방법이다.")]
        [SerializeField] private HashDataSO[] clips;

        [Tooltip("켜면 클립을 무작위 대신 이번 방문의 갈래(CustomerContext.PickVariant)로 고른다. 춤마다 대사가 다를 때 SpeechState의 lineIndices와 같은 순서로 클립을 넣는다.")]
        [SerializeField] private bool matchVariant;

        [Tooltip("재생 시간(초). 0이면 Phase가 바뀌거나 인터럽트가 들어올 때까지 계속한다.")]
        [SerializeField, Min(0f)] private float duration = 6f;

        [Tooltip("재생 시간에 더해지는 무작위 흔들림(초). 여러 명이 동시에 시작하고 동시에 끝나지 않게 한다.")]
        [SerializeField, Min(0f)] private float jitter = 1f;

        [Tooltip("켜면 시작 전에 차 쪽을 돌아본다. 가게를 등지고 춤추는 그림을 피할 때 쓴다.")]
        [SerializeField] private bool faceCar;

        [Tooltip("몸을 돌리는 각속도(도/초).")]
        [SerializeField, Min(1f)] private float turnSpeed = 360f;

        [Header("사운드")]
        [Tooltip("연출을 시작할 때 낼 소리. 화내는 몸짓이면 고함, 춤이면 흥얼거림처럼 클립에 맞춰 넣는다. loop를 켜면 연출이 끝날 때 함께 끈다.")]
        [SerializeField] private SoundClipSo startSound;

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[{nameof(PlayAnimationState)}] {Ctx.Customer.name}에 재생할 클립이 없어 건너뜁니다.", Ctx.Customer);
                return VisitOutcome.Blocked;
            }

            // 걷다가 곧장 연출로 넘어오면 이동 모듈이 경로를 붙들고 있어 연출 중에도 몸이 끌려간다.
            Ctx.Customer.Mover?.Stop();

            if (faceCar && Ctx.Visit?.Car != null)
            {
                await FaceTowards(Ctx.Visit.Car.transform.position, turnSpeed, ct);
            }

            HashDataSO clip = clips[matchVariant ? Ctx.PickVariant(clips.Length) : Random.Range(0, clips.Length)];

            float time = duration;
            if (time > 0f && jitter > 0f)
            {
                time += Random.Range(0f, jitter);
            }

            ISoundEmitter sound = Ctx.Customer.Sound;

            // 춤 음악처럼 연출보다 긴 소리는 loop를 켜 두면 연출이 끝날 때 함께 끈다.
            bool looping = startSound != null && startSound.loop;
            if (looping)
            {
                sound?.PlayLoop(startSound);
            }
            else
            {
                sound?.Play(startSound);
            }

            try
            {
                await PlayAction(clip, time, ct);
            }
            finally
            {
                // 취소로 끊겨도 여기는 반드시 지난다. 빼먹으면 춤을 멈추고 걸어가는 내내 음악이 따라다닌다.
                if (looping && sound != null && sound.CurrentLoop == startSound)
                {
                    sound.StopLoop();
                }
            }

            return VisitOutcome.Done;
        }
    }
}
