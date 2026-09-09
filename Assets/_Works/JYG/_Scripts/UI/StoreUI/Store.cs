using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class Store : MonoBehaviour
    {
        [SerializeField] private Transform upgradeContentParent;
        [SerializeField] private UpgradeBlock upgradeBlock;
         public List<StoreItem> itemList = new List<StoreItem>(); //엑셀에 있는 리스트로 긁어와야 한다.

         private void Awake()
         {
             if(upgradeContentParent != null)
                 foreach (StoreItem item in itemList)
                 {
                     UpgradeBlock block = Instantiate(upgradeBlock, upgradeContentParent);
                     block.UpgradeInit();
                 }
         }
    }

    [Serializable]
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
}
