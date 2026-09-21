using System;
using UnityEngine;

namespace _Works.JYG._Scripts.Data_Container.Store
{
    [CreateAssetMenu(fileName = "new StoreValueContainer", menuName = "Data Container/Store Value Container")]
    public class StoreValueContainer : ScriptableObject, IDataContainer<float>
    {
        public event IDataContainer<float>.OnValueChangedEvent OnValueChanged;
        private float _value;

        public float Value
        {
            get => _value;
            set
            {
                if (Mathf.Approximately(value, _value)) return;
                OnValueChanged?.Invoke(_value, value);
                OnRawValueChanged?.Invoke(value);
                _value = value;
            }
        }

        public object RawValue => Value;
        public event Action<object> OnRawValueChanged;

        public StoreValueContainer GetInstance() => Instantiate(this);
    }
}
