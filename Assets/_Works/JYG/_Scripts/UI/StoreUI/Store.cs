using System;
using System.Collections.Generic;
using System.Linq;
using Resources.DataBase.Human_Data;
using Resources.DataBase.Upgrade_Data;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class Store : MonoBehaviour
    {
        [SerializeField] private Transform upgradeContentParent;    //업그레이드 블럭을 만들 때, 부모가 될 대상이다.
        [SerializeField] private UpgradeBlock upgradeBlock;         //업그레이드 블록의 프리팹임.
         private List<StoreItem> itemList = new List<StoreItem>();   //엑셀에 있는 리스트로 긁어와야 한다.

         private UpgradeDB upgradeDB;

         private void Awake()
         {
             upgradeDB = UnityEngine.Resources.Load<UpgradeDB>("DataBase/Upgrade Data/UpgradeDB");

             if (upgradeDB == null)
             {
                 Debug.LogError("UpgradeDB를 찾지 못했습니다. : " + gameObject.name);
                 return;
             }

             foreach (UpgradeData data in upgradeDB.UpgradeSheet)
             {
                 itemList.Add(new StoreItem
                 (
                     data.maxlv,
                     data.value,
                     data.lv1price,
                     new UpgradeValue(data.upgradetype, data.lv1value)
                     ));
             }
             
             if(upgradeContentParent != null)
                 foreach (StoreItem item in itemList)   //블럭의 수만큼 foreach 돌려준다.
                 {
                     UpgradeBlock block = Instantiate(upgradeBlock, upgradeContentParent);
                     block.UpgradeInit(item);   //Block Init에서는 레벨 칸 갯수, 이벤트 연결 작업을 해준다.
                 }
         }
    }

    public struct UpgradeValue
    {
        public UpgradeType ValueType;
        public float Value;

        public UpgradeValue(UpgradeType type, float value)
        {
            this.ValueType = type;
            this.Value = value;
        }
    }
    public struct StoreItem  //엑셀에 있는 DB데이터로 바꿔야 한다.
    {
        public int level;
        public string itemName;
        public int price;
        public UpgradeValue value;

        public StoreItem(int level, string itemName, int price, UpgradeValue value)
        {
            this.level = level;
            this.itemName = itemName;
            this.price = price;
            this.value = value;
        }
    }
}
