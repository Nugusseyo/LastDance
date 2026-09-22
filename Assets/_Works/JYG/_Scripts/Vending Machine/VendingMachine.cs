using System;
using System.Collections.Generic;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.Vending_Machine;
using UnityEngine;

namespace _Works.JYG.Data.Vending_Machine
{
    public class VendingMachine : MonoBehaviour
    {
        public List<VendingItemData> vendingItems = new List<VendingItemData>();
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Transform parent;
        [SerializeField] private IntegerDataContainer moneyManager;
        
        private List<GameObject> items = new List<GameObject>();
        
        private void Awake()
        {
            if (vendingItems.Count > 9)
                Debug.LogWarning("자판기의 최대 아이템 숫자를 넘어섰습니다.");

            for (int i = 0; i < vendingItems.Count; i++)
            {
                VendingItem item = Instantiate(itemPrefab, parent)
                    .GetComponent<VendingItem>();
                item.InitializeItem(vendingItems[i], moneyManager);
                items.Add(item.gameObject);
            }
        }
    }
}
