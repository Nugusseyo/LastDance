using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class CiderEffect : MonoBehaviour, IItemEffect
    {
        [SerializeField] private float duration = 5f;

        public bool IsRatingDropImmune { get; private set; }

        public void Apply()
        {
            StopAllCoroutines();
            StartCoroutine(ImmunityRoutine());
        }

        private System.Collections.IEnumerator ImmunityRoutine()
        {
            IsRatingDropImmune = true;
            yield return new WaitForSeconds(duration);
            IsRatingDropImmune = false;
        }
    }
}
