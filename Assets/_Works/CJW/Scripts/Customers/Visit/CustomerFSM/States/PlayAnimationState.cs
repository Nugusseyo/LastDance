using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DevLib.AnimatorSystem;
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

        [Tooltip("재생 시간(초). 0이면 Phase가 바뀌거나 인터럽트가 들어올 때까지 계속한다.")]
        [SerializeField, Min(0f)] private float duration = 6f;

        [Tooltip("재생 시간에 더해지는 무작위 흔들림(초). 여러 명이 동시에 시작하고 동시에 끝나지 않게 한다.")]
        [SerializeField, Min(0f)] private float jitter = 1f;

        [Tooltip("켜면 시작 전에 차 쪽을 돌아본다. 가게를 등지고 춤추는 그림을 피할 때 쓴다.")]
        [SerializeField] private bool faceCar;

        [Tooltip("몸을 돌리는 각속도(도/초).")]
        [SerializeField, Min(1f)] private float turnSpeed = 360f;

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

            HashDataSO clip = clips[Random.Range(0, clips.Length)];

            float time = duration;
            if (time > 0f && jitter > 0f)
            {
                time += Random.Range(0f, jitter);
            }

            await PlayAction(clip, time, ct);

            return VisitOutcome.Done;
        }
    }
}
