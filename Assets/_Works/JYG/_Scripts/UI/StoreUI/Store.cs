using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class Store : MonoBehaviour
    {
        [SerializeField] private Transform upgradeContentParent;    //업그레이드 블럭을 만들 때, 부모가 될 대상이다.
        [SerializeField] private UpgradeBlock upgradeBlock;         //업그레이드 블록의 프리팹임.
         public List<StoreItem> itemList = new List<StoreItem>();   //엑셀에 있는 리스트로 긁어와야 한다.

         private void Awake()
         {
             if(upgradeContentParent != null)
                 foreach (StoreItem item in itemList)   //블럭의 수만큼 foreach 돌려준다.
                 {
                     UpgradeBlock block = Instantiate(upgradeBlock, upgradeContentParent);
                     block.UpgradeInit();   //Block Init에서는 레벨 칸 갯수, 이벤트 연결 작업을 해준다.
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
