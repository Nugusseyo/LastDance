using System;
using System.Collections.Generic;
using System.Linq;
using _Works.JYG._Scripts.Data_Container;
using _Works.JYG._Scripts.Util;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI
{
    public class UIDataLinker : MonoBehaviour
    {
        public List<DataLinkWrappers> dataLinkWrappers = new List<DataLinkWrappers>();
        private List<(IDataContainer, Action<object>)> dataContainersHandler;

        private void Awake()
        {
            if (dataLinkWrappers.Count <= 0) return;
            
            dataContainersHandler = new List<(IDataContainer, Action<object>)>();
            
            foreach (DataLinkWrappers wrapper in dataLinkWrappers)
            {
                IDataContainer container = wrapper.targetData.GetInterface();
                Action<object> handler = (newValue) => wrapper.targetTextField.text = newValue.ToString(); //이거 지역 함수로 쓰는 게 있네. 엄청 신기함.
                container.OnRawValueChanged += handler;
                dataContainersHandler.Add((container, handler));
                //void Handler(object newValue) => wrapper.targetTextField.text = newValue.ToString();
                //dataContainersHandler.Add((container, Handler));                                          // 이거 ㄷㄷ
            }
        }

        private void OnDestroy()
        {
            if (dataContainersHandler.Count > 0)
            {
                foreach (var item in dataContainersHandler)
                {
                    item.Item1.OnRawValueChanged -= item.Item2;
                }
            }
        }

        public IEnumerable<IDataContainer<T>> GetDataContainers<T>()
        {
            return dataLinkWrappers
                .Select(x => x.targetData.GetInterface())
                .OfType<IDataContainer<T>>();
        }
    }
    
    [Serializable]
    public class DataLinkWrappers
    {
        public SerializableInterface<IDataContainer> targetData;
        public TextMeshProUGUI targetTextField;
    }
}
