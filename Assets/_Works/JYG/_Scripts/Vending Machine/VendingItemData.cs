using _Works.KDH._01.Scripts.Vending;
using UnityEngine;

namespace _Works.JYG._Scripts.Vending_Machine
{
    [CreateAssetMenu(fileName = "new Item data", menuName = "Data/Vending Item Data")]
    public class VendingItemData : ScriptableObject
    {
        public int index;
        public Sprite vendingItemSprite;
        public int price;
        public VendingItemSO itemSO;
    }
}
