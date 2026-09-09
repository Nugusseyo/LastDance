using System;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class UpgradeBlock : MonoBehaviour
    {
        [SerializeField] private UpgradeBarInitializer barInitializer;
        //[SerializeField] private 여기에클래스입력 이름

        public void UpgradeInit() //Class 받아야 함.
        {
            if(barInitializer.enabled)
                barInitializer.InitializeBar(10);
            else
                Debug.LogWarning("이미 Initialize된 Bar Initializer에 초기설정 시도함. : " + gameObject.name);
        }
    }
}
