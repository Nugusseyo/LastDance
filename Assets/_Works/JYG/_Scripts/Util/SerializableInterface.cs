using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Works.JYG._Scripts.Util
{
    //Unity Technology - TowerDefense Template에서 참고해 만듦.
    public interface ISerializableInterface
    {
    }

    [Serializable]
    public class SerializableInterface<T> where T : ISerializableInterface
    {
        public Object targetObject;
        private T referenceObject;

        public T GetInterface()
        {
            if (targetObject != null && referenceObject == null)
            {
                referenceObject = (T)(ISerializableInterface)targetObject;
            }
            if(targetObject == null || referenceObject == null)
                Debug.LogWarning("Serializable Interface가 Null입니다. Type : " + typeof(T).ToString());
            
            return referenceObject;
        }
    }
}
