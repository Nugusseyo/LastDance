using System;
using _Works.JYG._Scripts.Events;
using _Works.JYG._Scripts.UI.KeyHint;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.Keyboard_Viewer
{
    public class KeyHintLinker : MonoBehaviour
    {
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private KeyHintType hintType;

        public void InvokeSubEvent()
        {
            if (eventChannel != null && hintType != KeyHintType.None)
            {
                eventChannel.RaiseEvent(UIEvents.KeyHintType.Init(hintType, this, true));
            }
        }

        public void InvokeDeSubEvent()
        {
            if (eventChannel != null && hintType != KeyHintType.None)
            {
                eventChannel.RaiseEvent(UIEvents.KeyHintType.Init(hintType, this, false));
            }
        }
    }
}
