using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using _Works.JJH._02_Scripts.Agents.Modules;
using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Animation
{
    /// <summary>연출 애니메이션 재생기. 클립을 트는 일만 하고 언제 트는지는 상태가 정한다.
    /// 애니메이터를 직접 쥐지 않고 <see cref="IRenderer"/>를 거치는 이유는, 렌더러 모듈이 있는 프리팹에서
    /// 두 곳이 같은 Animator에 서로 다른 크로스페이드를 걸지 않게 하기 위해서다.</summary>
    [DisallowMultipleComponent]
    public sealed class ActionAnimatorModule : AbstractModule, IActionAnimator
    {
        [Tooltip("연출 클립을 섞어 넣는 시간(초).")]
        [SerializeField, Min(0f)] private float crossFade = 0.1f;

        [Tooltip("연출을 접을 때 돌아갈 상태. 비워두면 이동 모듈이 다음 프레임에 알아서 걷기·서기를 다시 튼다.")]
        [SerializeField] private HashDataSO exitClip;

        private IRenderer _renderer;
        private Animator _animator;

        public bool IsPlaying { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _renderer = owner != null ? owner.GetModule<IRenderer>() : null;
            _animator = _renderer?.Animator;

            if (_animator == null && owner != null)
            {
                _animator = owner.GetComponentInChildren<Animator>(true);
            }

            if (_animator == null)
            {
                // 여기서 막지 않으면 춤이나 주먹질이 조용히 아무것도 하지 않고, 손님은 가만히 서 있기만 한다.
                Debug.LogError($"[{nameof(ActionAnimatorModule)}] {name}에 Animator가 없어 연출을 재생하지 못합니다.", this);
            }
        }

        public void Begin(HashDataSO clip)
        {
            if (clip == null || clip.HashValue == 0)
            {
                Debug.LogWarning($"[{nameof(ActionAnimatorModule)}] {name}에 재생할 클립이 비어 있어 연출을 건너뜁니다.", this);
                return;
            }

            IsPlaying = true;
            Play(clip);
        }

        public void End()
        {
            if (!IsPlaying)
            {
                return;
            }

            IsPlaying = false;

            // 이동 모듈이 다음 프레임에 걷기·서기를 다시 틀어 준다. exitClip은 이동 모듈이 없는 프리팹을 위한 보험이다.
            if (exitClip != null)
            {
                Play(exitClip);
            }
        }

        public async UniTask PlayFor(HashDataSO clip, float duration, CancellationToken ct)
        {
            Begin(clip);

            try
            {
                if (duration > 0f)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
                }
                else
                {
                    // 취소로만 벗어난다. 예외를 던지지 않고 조용히 끝난다.
                    await UniTask.WaitUntilCanceled(ct);
                }
            }
            finally
            {
                // 취소로 끊겨도 여기는 반드시 지난다. 빼먹으면 손님이 걷는 내내 춤 클립을 붙들고 있다.
                End();
            }
        }

        private void Play(HashDataSO clip)
        {
            if (_renderer != null)
            {
                _renderer.PlayClip(clip.HashValue, 0f, crossFade);
                return;
            }

            if (_animator != null && _animator.isActiveAndEnabled)
            {
                _animator.CrossFadeInFixedTime(clip.HashValue, crossFade, 0, 0f);
            }
        }
    }
}
