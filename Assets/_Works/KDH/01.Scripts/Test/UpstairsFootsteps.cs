using UnityEngine;

namespace _Works.KDH._01.Scripts
{
    [RequireComponent(typeof(AudioSource))]
    public class UpstairsFootsteps : MonoBehaviour
    {
        [SerializeField] private AudioClip footstepLoop;
        [SerializeField] private float delay = 5f;
        [SerializeField] private bool playOnce = true;

        private AudioSource source;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.clip = footstepLoop;
            source.loop = !playOnce;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
        }

        private void Start()
        {
            Invoke(nameof(PlayFootsteps), delay);
        }

        private void PlayFootsteps()
        {
            if (footstepLoop == null) return;
            source.Play();
        }
    }
}
