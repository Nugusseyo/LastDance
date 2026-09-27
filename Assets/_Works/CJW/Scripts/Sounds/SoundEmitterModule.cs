using DevLib.EventChannelSystem;
using DevLib.ModuleSystem;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Sounds
{
    /// <summary>소리를 <see cref="SoundEvents.PlaySoundEvent"/>로 사운드 채널에 올린다. 재생은 씬의 SoundManager가 한다.
    /// 상태·모듈마다 채널을 따로 물리지 않도록 주인(손님·차)에 하나만 붙인다.</summary>
    [DisallowMultipleComponent]
    public sealed class SoundEmitterModule : AbstractModule, ISoundEmitter
    {
        [Tooltip("SoundManager가 듣고 있는 채널. 비워두면 소리를 내지 않는다.")]
        [SerializeField] private EventChannelSO soundChannel;

        /// <summary>반복 소리 채널 번호의 시작값. SoundManager는 같은 번호의 반복 소리를 하나로 묶으므로
        /// 차마다 번호가 달라야 한다. 다른 곳에서 손으로 정한 작은 번호와 겹치지 않게 크게 잡는다.</summary>
        private const int LoopChannelBase = 100000;

        private static int _nextLoopChannel = LoopChannelBase;

        private int _loopChannel;

        public bool IsLooping { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            if (soundChannel == null)
            {
                Debug.LogWarning($"[{nameof(SoundEmitterModule)}] {name}에 사운드 채널이 없어 소리를 내지 않습니다.", this);
            }
        }

        private void OnDisable()
        {
            // 풀로 돌아가는 차가 반복 소리를 남겨 두면 빈 자리에서 엔진 소리가 계속 난다.
            StopLoop();
        }

        public void Play(SoundClipSo clip)
        {
            Play(clip, OwnerTransform.position);
        }

        public void Play(SoundClipSo clip, Vector3 position)
        {
            if (!CanPlay(clip))
            {
                return;
            }

            // 반복 클립을 여기로 틀면 끌 방법이 없다. 반복 소리는 PlayLoop로 튼다.
            if (clip.loop)
            {
                Debug.LogWarning($"[{nameof(SoundEmitterModule)}] {clip.name}은(는) 반복 재생 클립이라 틀지 않습니다. PlayLoop를 쓰거나 loop를 끄세요.", clip);
                return;
            }

            soundChannel.RaiseEvent(SoundEvents.PlaySoundEvent.Init(position, clip));
        }

        public void PlayLoop(SoundClipSo clip)
        {
            if (!CanPlay(clip))
            {
                return;
            }

            if (!clip.loop)
            {
                Debug.LogWarning($"[{nameof(SoundEmitterModule)}] {clip.name}은(는) loop가 꺼져 있어 반복 소리로 틀지 않습니다.", clip);
                return;
            }

            if (_loopChannel == 0)
            {
                _loopChannel = _nextLoopChannel++;
            }

            // 같은 채널로 다시 틀면 SoundManager가 앞의 소리를 끊고 바꿔 끼운다.
            Transform follow = OwnerTransform;
            soundChannel.RaiseEvent(SoundEvents.PlaySoundEvent.Init(follow.position, clip, _loopChannel, follow));
            IsLooping = true;
        }

        public void StopLoop()
        {
            if (!IsLooping)
            {
                return;
            }

            IsLooping = false;

            if (soundChannel != null)
            {
                soundChannel.RaiseEvent(SoundEvents.StopSoundEvent.Init(_loopChannel));
            }
        }

        private Transform OwnerTransform => _owner != null ? _owner.transform : transform;

        private bool CanPlay(SoundClipSo clip)
        {
            // 오디오 클립까지 봐야 한다. SoundPlayer는 clip.length를 바로 읽어 비어 있으면 예외가 난다.
            return clip != null && clip.clip != null && soundChannel != null;
        }
    }
}
