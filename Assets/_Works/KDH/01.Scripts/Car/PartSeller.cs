using UnityEngine;
using _Works.KDH._01.Scripts.ItemType;

namespace _Works.KDH._01.Scripts.Car
{
    public class PartSeller : MonoBehaviour
    {

        [SerializeField] private int defaultPrice = 50;
        [SerializeField] private WalletEffect walletEffect;


        public int SellPart(GameObject part)
        {
            if (part == null)
            {
                return 0;
            }

            int price = GetPartPrice(part);

            if (walletEffect != null && walletEffect.IsMoneyDoubled)
            {
                price *= 2;
            }

            Destroy(part);

            return price;
        }

        private int GetPartPrice(GameObject part)
        {
            PartValue partValue = part.GetComponent<PartValue>();

            if (partValue != null)
            {
                return partValue.SellPrice;
            }
            return defaultPrice;
        }
    }
}
