using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Works.JYG._Scripts.Data_Container.Money;
using Resources.DataBase.Human_Data;
using Resources.DataBase.Upgrade_Data;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class Store : MonoBehaviour
    {
         [SerializeField] private IntegerDataContainer moneyManager;
         
         [SerializeField] private Transform upgradeContentParent;    //업그레이드 블럭을 만들 때, 부모가 될 대상이다.
         [SerializeField] private UpgradeBlock upgradeBlock;         //업그레이드 블록의 프리팹임.
         [SerializeField] private List<StoreItem> itemList = new List<StoreItem>();  //엑셀에 있는 리스트로 긁어와야 한다.
         private List<StoreItem> saveLoadData;     //저장된 파일로부터 가져온 이전 게임의 레벨 데이터

         private const string SaveFilePath = "storeData.json";  //저장할 json데이터의 이름
         public string SavePath => Path.Combine(Application.persistentDataPath, SaveFilePath);  //저장 루트 (윈도우나 apk가 지정해주는 폴더임)
         private UpgradeDB upgradeDB;
         
         private Dictionary<int, UpgradeBlock> upgradeDict = new();

         private void Awake()
         {
             upgradeDB = UnityEngine.Resources.Load<UpgradeDB>("DataBase/Upgrade Data/UpgradeDB");
             
             if (upgradeDB == null)
             {
                 Debug.LogError("UpgradeDB를 찾지 못했습니다. : " + gameObject.name);
                 return;
             }
             
             LoadListInitialize();  // 저장된 LoadList 값을 불러온다.
             InitializeBlock();
             ApplyBlockData();
         }

         private void OnDestroy()
         {
             SaveList();
             
             foreach (UpgradeBlock block in upgradeDict.Values)
                 block.DestroyRealStoreValue();
         }

         private void ApplyBlockData()
         {
             if (saveLoadData.Count == 0) return;

             foreach (StoreItem data in saveLoadData)
             {
                 StoreItem loadItem = saveLoadData[data.index];
                 UpgradeBlock block = upgradeDict[loadItem.index];
                 Debug.Log(loadItem.itemName + " : " + loadItem.maxlevel);
                 block.UpgradeRequest(loadItem.curLevel, true);
             }
         }

         private void InitializeBlock()
         {
             foreach (UpgradeData data in upgradeDB.UpgradeSheet)
             {
                 itemList.Add(new StoreItem
                 (
                     data.index,
                     data.maxlv,
                     0,
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
                     upgradeDict.Add(item.index, block);
                 }
         }

         private void LoadListInitialize()
         {
             Debug.Log("LoadPath : " + SavePath);

             if (!File.Exists(SavePath))
             {
                 Debug.Log("저장된 세이브 파일이 없어서 기본 값을 사용함.");
                 saveLoadData = new();
                 return;
             }
             
             string jsonFile = File.ReadAllText(SavePath);
             
             StoreValueForJson save = JsonUtility.FromJson<StoreValueForJson>(jsonFile);
             saveLoadData = new(save.saveData);
             Debug.Log("데이터 로드 성공.");
         }

         private void SaveList()
         {
             try
             {
                 StoreValueForJson jsonClass = new()
                 {
                     saveData = itemList
                 };
                 
                 string saveJson = JsonUtility.ToJson(jsonClass);
                 Debug.Log("<color=green> Save : </color>" + saveJson);
                 File.WriteAllText(SavePath, saveJson);
             }
             catch (Exception e)
             {
                 Debug.LogError("상점 정보 저장에 실패했습니다.");
             }
         }

         public void TryUpgradeItem(int index)
         {
             if (index <= 0 || index >= itemList.Count)
                 return;
             
             if(!CanUpgradeItem(itemList[index]))
             {
                 Debug.Log("아이템 구매에 실패했습니다.");
                 return;
             }
             StoreItem curItem = itemList[index];
             upgradeDict[index].UpgradeRequest(curItem.curLevel + 1, false);
             
             
         }
         private bool CanUpgradeItem(StoreItem item) => moneyManager.Value >= item.price;
        
    }

    [Serializable]
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
    
    [Serializable]
    public struct StoreItem  //엑셀에 있는 DB데이터로 바꿔야 한다.
    {
        public int index;
        public int maxlevel;
        public int curLevel;
        public string itemName;
        public int price;
        public UpgradeValue value;

        public StoreItem(int index, int maxlevel, int curLevel, string itemName, int price, UpgradeValue value)
        {
            this.index = index;
            this.maxlevel = maxlevel;
            this.curLevel = curLevel;
            this.itemName = itemName;
            this.price = price;
            this.value = value;
        }

        public StoreItem(StoreItem item)
        {
            index = item.index;
            maxlevel = item.maxlevel;
            curLevel = item.curLevel;
            itemName = item.itemName;
            price = item.price;
            value = item.value;
        }
    }

    [Serializable]
    public class StoreValueForJson
    {
        public List<StoreItem> saveData = new();
    }
}
