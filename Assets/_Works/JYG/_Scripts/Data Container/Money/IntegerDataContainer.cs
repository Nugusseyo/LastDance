using System;
using _Works.JYG._Scripts.SaveSystem;
using UnityEngine;

namespace _Works.JYG._Scripts.Data_Container.Money
{
    [CreateAssetMenu(fileName = "new Integer Manager", menuName = "Data Container/Integer Data Manager")]
    public class IntegerDataContainer : ScriptableObject, IDataContainer<int>, ISavableData
    {
        public event IDataContainer<int>.OnValueChangedEvent OnValueChanged;    //T밸류(int) 변경 시 동작하는 액션
        private int _value;

        public int maxValue = int.MaxValue;
        public int minValue = int.MinValue;

        public int Value
        {
            get => _value;
            set
            {
                int clampedValue = Mathf.Clamp(value, minValue, maxValue);
                
                if (clampedValue == _value) return;

                OnValueChanged?.Invoke(clampedValue, _value);
                OnRawValueChanged?.Invoke(clampedValue);
                _value = clampedValue;
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
            
            DataSaveSystem.SetSaveData<IntegerDataForJson>(key, saveData);
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
