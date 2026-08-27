using System;
using _Works.JYG._Scripts.SaveSystem;
using UnityEngine;

namespace _Works.JYG._Scripts.Data_Container.Money
{
    [CreateAssetMenu(fileName = "new Integer Manager", menuName = "Data Container/Integer Data Manager")]
    public class IntegerDataContainer : ScriptableObject, IDataContainer<int>, ISavableData
    {
        public event IDataContainer<int>.OnValueChangedEvent OnValueChanged;
        private int _value;

        public int Value
        {
            get => _value;
            set
            {
                if (value == _value) return;
                OnValueChanged?.Invoke(_value, value);
                OnRawValueChanged?.Invoke(value);
                _value = value;
            }
        }

        public object RawValue => Value;
        public event Action<object> OnRawValueChanged;

        public void InitializeData(string key)
        {
            IntegerDataForJson data = DataSaveSystem.GetSaveData<IntegerDataForJson>(key);
            if (data == null)
                _value = 0;
            else
                _value = data.value;
            
            OnValueChanged?.Invoke(_value, 0);
            OnRawValueChanged?.Invoke(_value);
        }

        public void SaveData(string key)
        {
            IntegerDataForJson saveData = new IntegerDataForJson(_value);
            Debug.Log($"IntValue : {saveData.value}");
            string value = JsonUtility.ToJson(saveData.value);
            
            DataSaveSystem.SetSaveData(key, value);
        }
    }

    [Serializable]
    public class IntegerDataForJson
    {
        public IntegerDataForJson(int value)
        {
            this.value = value;
        }
        public int value;
    }
    
}
