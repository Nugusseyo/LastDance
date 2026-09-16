using UnityEngine;

namespace _Works.JYG.Data.Vending_Machine
{
    [CreateAssetMenu(fileName = "new Item data", menuName = "Data/Vending Item Data")]
    public class VendingItemData : ScriptableObject
    {
        public Sprite vendingItemSprite;
        public int price;
    }
}
