using System;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.JYG._Scripts.Debugging
{
    public class GaugeDebugger : MonoBehaviour
    {
        public EventChannelSO eventChannel;
        private float currentFloat = 1;
        [SerializeField] private float increaseSpeed = 0.5f;
        private void Update()
        {
            if (Keyboard.current.spaceKey.isPressed)
            {
                currentFloat = Mathf.Clamp01(currentFloat + Time.deltaTime * increaseSpeed);
                eventChannel.RaiseEvent(UIEvents.GaugeEvent.Init(currentFloat));
            }
            else
            {
                currentFloat = Mathf.Clamp01(currentFloat - Time.deltaTime * increaseSpeed);
                eventChannel.RaiseEvent(UIEvents.GaugeEvent.Init(currentFloat));
            }
        }
    }
}
