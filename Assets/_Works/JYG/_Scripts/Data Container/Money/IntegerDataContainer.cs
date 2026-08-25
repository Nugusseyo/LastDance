using System;
using UnityEngine;

namespace _Works.JYG._Scripts.Data_Container.Money
{
    [CreateAssetMenu(fileName = "new Integer Manager", menuName = "Data Container/Integer Data Manager")]
    public class IntegerDataContainer : ScriptableObject, IDataContainer<int>
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
        public void InitializeData(int value)
        {
            _value = value;
            OnValueChanged?.Invoke(_value, 0);
            OnRawValueChanged?.Invoke(value);
        }

        public object RawValue => Value;
        public event Action<object> OnRawValueChanged;
    }
}
