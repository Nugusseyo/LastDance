using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class Store : MonoBehaviour
    {
         public List<StoreItem> itemList = new List<StoreItem>(); //엑셀에 있는 리스트로 긁어와야 한다.

         private void Awake()
         {
             int index = 0;
             foreach (StoreBlock block in GetComponentsInChildren<StoreBlock>())
             {
                 block.InitializeItem(new StoreItem(index++, "아이템"+index, 2000 * (index + 1)), this); //나중에 저장된 값을 기반으로 들고오게 해야한다.
             }
         }
    }

    public class StoreItem  //엑셀에 있는 DB데이터로 바꿔야 한다.
    {
        public int level;
        public string itemName;
        public int price;

        public StoreItem(int level, string itemName, int price)
        {
            this.level = level;
            this.itemName = itemName;
            this.price = price;
        }
    }

    public class StoreBlock : MonoBehaviour
    {
        private Store _owner;
        
        [SerializeField] private TextMeshProUGUI itemTmp;
        [SerializeField] private TextMeshProUGUI priceTmp;
        [SerializeField] private TextMeshProUGUI levelTmp;
        public void InitializeItem(StoreItem itemData, Store owner)
        {
            _owner = owner;
            itemTmp.text = itemData.itemName;
            priceTmp.text = itemData.price + "$";
            levelTmp.text = itemData.level + " Lv";
        }
    }
}
