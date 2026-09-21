using UnityEngine;

namespace DevLib.SoundSystem
{
    public enum AudioTypes
    { Sfx, Music }
    
    [CreateAssetMenu(fileName = "SoundClip", menuName = "Lib/Sound/Clip", order = 0)]
    public class SoundClipSo : ScriptableObject
    {
        public AudioTypes audioType;
        public AudioClip clip;
        public bool loop = false;
        public bool randomizePitch = false;

        [Range(0, 1f)]
        public float randomPitchModifier = 0.1f;
        [Range(0.1f, 2f)]
        public float volume = 1f;
        [Range(0.1f, 3f)]
        public float pitch = 1f;
        
        public float startTime = 0f;
        public float endTime = 0f;
    }
}