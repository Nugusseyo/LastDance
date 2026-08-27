using System;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.SaveSystem;
using _Works.JYG._Scripts.Util;
using UnityEngine;

namespace _Works.JYG._Scripts.GameModule
{
    public class GameManager : MonoBehaviour
    {
        public InitDataWrap[] initDatas;

        private void Start()
        {
            foreach (InitDataWrap wrap in initDatas)
            {
                SerializableInterface<ISavableData> serializableInterface = wrap.data;
                ISavableData savable = serializableInterface.GetInterface();
                
                Debug.Log($"{wrap.key} : {DataSaveSystem.GetSaveData<IntegerDataForJson>(wrap.key).value}");

                savable.InitializeData(wrap.key);
            }
        }

        private void OnDisable()
        {
            foreach (InitDataWrap wrap in initDatas)
            {
                ISavableData savable = wrap.data.GetInterface();
                Debug.Log($"{wrap.key} : {DataSaveSystem.GetSaveData<IntegerDataForJson>(wrap.key).value}");
                savable.SaveData(wrap.key);
            }
        }
    }

    [Serializable]
    public class InitDataWrap
    {
        public SerializableInterface<ISavableData> data;
        public string key;
    }
}