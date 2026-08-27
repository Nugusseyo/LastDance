using System;
using _Works.JYG._Scripts.Util;

namespace _Works.JYG._Scripts.Data_Container
{
    public interface IDataContainer<T> : IDataContainer
    {
        T Value { get; }    //T : Data Type

        delegate void OnValueChangedEvent(T newValue, T oldValue);  //데이터(Value)변경 시 동작하는 이벤트

        event OnValueChangedEvent OnValueChanged;
    }

    public interface IDataContainer : ISerializableInterface
    {
        object RawValue { get; }                                      //UI에서 자료형 상관 없이 보이게 하기 위한 RawValue
        event Action<object> OnRawValueChanged;                       //RawValueChange시 발행
    }
}
