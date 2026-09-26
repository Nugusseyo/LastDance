using System;
using _Works.JYG._Scripts.Data_Container.Money;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.JYG._Scripts.Debugging
{
    public class DataDebugger : MonoBehaviour
    {
        public IntegerDataContainer dataContainer;
        private void Update()
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                dataContainer.Value += 1000;
            }
            
            if (Keyboard.current.enterKey.wasPressedThisFrame)
            {
                dataContainer.Value -= 1000;
            }
        }
    }
}
