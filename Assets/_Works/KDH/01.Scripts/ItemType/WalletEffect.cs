using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class WalletEffect : MonoBehaviour, IItemEffect
    {
        [SerializeField] private float duration = 5f;

        public bool IsMoneyDoubled { get; private set; }

        public void Apply()
        {
            StopAllCoroutines();
            StartCoroutine(DoubleMoneyRoutine());
        }

        private System.Collections.IEnumerator DoubleMoneyRoutine()
        {
            IsMoneyDoubled = true;
            yield return new WaitForSeconds(duration);
            IsMoneyDoubled = false;
        }
    }
}
