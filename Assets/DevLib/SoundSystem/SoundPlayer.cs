using System;
using System.Threading.Tasks;
using DevLib.ObjectPool.Runtime;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace DevLib.SoundSystem
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundPlayer : PoolableMono
    {
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup musicGroup;
        
        private AudioSource _audioSource;
        private Transform _follow;
        
        public event Action<SoundPlayer> OnSoundFinished;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        /// <summary>소리가 따라다닐 대상을 정한다. null이면 제자리에 머문다.</summary>
        public void SetFollow(Transform follow)
        {
            _follow = follow;
        }

        private void LateUpdate()
        {
            if (_follow != null)
            {
                transform.position = _follow.position;
            }
        }

        public void PlaySound(SoundClipSo clipData)
        {
            if (clipData.audioType == AudioTypes.Sfx)
            {
                _audioSource.outputAudioMixerGroup = sfxGroup;
            }
            else if (clipData.audioType == AudioTypes.Music)
            {
                _audioSource.outputAudioMixerGroup = musicGroup;
            }

            _audioSource.volume = clipData.volume;
            _audioSource.pitch = clipData.pitch;
            if (clipData.randomizePitch)
            {
                _audioSource.pitch += Random.Range(-clipData.randomPitchModifier, clipData.randomPitchModifier);
            }
            _audioSource.clip = clipData.clip;
            _audioSource.loop = clipData.loop;

            float startTime = clipData.startTime;
            float endTime   = clipData.endTime > startTime ? clipData.endTime : clipData.clip.length;

            _audioSource.timeSamples = Mathf.RoundToInt(startTime * clipData.clip.frequency);
            _audioSource.Play();

            if (!clipData.loop)
            {
                float duration = (endTime - startTime) / Mathf.Abs(_audioSource.pitch);
                _ = DisableSoundTimer(duration + 0.2f);
            }
        }
        private async Task DisableSoundTimer(float time)
        {
            await Awaitable.WaitForSecondsAsync(time);
            _audioSource.Stop();
            _follow = null;
            OnSoundFinished?.Invoke(this);
        }

        public void ForceStopSound()
        {
            _audioSource.Stop();
            _follow = null;
        }

    }
}