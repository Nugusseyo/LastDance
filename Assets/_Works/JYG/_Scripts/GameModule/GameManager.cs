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
            foreach (InitDataWrap wrap in initDatas)    //등록된 이니셜라이즈가 꼭 필요한 데이터들의 함수를 실행한다. (주로 머니매니저와 같은 데이터)
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
                savable.SaveData(wrap.key);
                Debug.Log($"{wrap.key} : {DataSaveSystem.GetSaveData<IntegerDataForJson>(wrap.key).value}");
            }
        }
    }

    [Serializable]
    public class InitDataWrap   //Savable 데이터들을 담게 해주는 Wrapper
    {
        public SerializableInterface<ISavableData> data;
        public string key;
    }
}