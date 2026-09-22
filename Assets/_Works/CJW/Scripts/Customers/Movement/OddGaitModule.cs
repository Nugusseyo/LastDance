using _Works.CJW.Scripts.ManagingAgents;
using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Movement
{
    /// <summary>이상하게 걷는 손님의 걸음걸이. 걷기 클립을 갈아끼우고, 가려는 방향에서 좌우로 비껴 보게 해서 비틀거리게 만든다.
    /// 목적지는 손대지 않으므로 아무리 비틀거려도 결국 갈 곳에는 도착한다 — 길을 잃게 만들면 방문이 굳는다.</summary>
    [DisallowMultipleComponent]
    public sealed class OddGaitModule : AbstractModule, IGait, IUpdate
    {
        [Tooltip("걸을 때 쓸 클립. 비워두면 이동 모듈의 기본 걷기를 그대로 쓰고 흔들림만 더한다.")]
        [SerializeField] private HashDataSO walkClip;

        [Tooltip("좌우로 비껴 보는 최대 각도(도). 0이면 클립만 갈아끼우고 흔들지 않는다.")]
        [SerializeField, Min(0f)] private float swayDegrees = 25f;

        [Tooltip("좌우로 오가는 빠르기(초당 왕복 수).")]
        [SerializeField, Min(0f)] private float swaySpeed = 0.6f;

        [Tooltip("흔들림에 섞는 불규칙함. 0이면 정확한 사인파라 기계적으로 보인다.")]
        [SerializeField, Range(0f, 1f)] private float irregularity = 0.35f;

        [Tooltip("시작할 때부터 켜 둘지. 끄고 시작해 상태가 도중에 켜게 할 수도 있다.")]
        [SerializeField] private bool activeOnStart = true;

        /// <summary>흔들림의 위상. 손님마다 다른 값에서 시작해야 여러 명이 같은 박자로 흔들리지 않는다.</summary>
        private float _phase;

        /// <summary>불규칙함을 만드는 두 번째 파동의 위상. 주기를 어긋나게 해서 같은 모양이 반복되지 않게 한다.</summary>
        private float _noisePhase;

        public bool IsActive { get; private set; }

        public HashDataSO WalkClip => walkClip;

        public float SwayAngle { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            IsActive = activeOnStart;

            // 풀에서 여러 명이 한꺼번에 나오면 위상이 같아 군무처럼 보인다. 개체마다 어긋나게 둔다.
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _noisePhase = Random.Range(0f, Mathf.PI * 2f);
        }

        public void SetActive(bool value)
        {
            IsActive = value;

            if (!value)
            {
                SwayAngle = 0f;
            }
        }

        public void OnUpdate(float dt)
        {
            if (!IsActive || swayDegrees <= 0f)
            {
                SwayAngle = 0f;
                return;
            }

            float step = swaySpeed * Mathf.PI * 2f * dt;
            _phase += step;

            // 황금비에 가까운 배수를 써서 두 파동의 주기가 맞아떨어지지 않게 한다. 맞아떨어지면 같은 모양이 계속 반복된다.
            _noisePhase += step * 1.618f;

            float wave = Mathf.Sin(_phase);
            float noise = Mathf.Sin(_noisePhase);

            SwayAngle = Mathf.Lerp(wave, wave * 0.5f + noise * 0.5f, irregularity) * swayDegrees;
        }
    }
}
