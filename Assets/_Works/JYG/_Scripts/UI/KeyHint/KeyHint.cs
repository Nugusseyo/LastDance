using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.KeyHint
{
    public class KeyHint : MonoBehaviour
    {
        private Dictionary<object, KeyHintType> hintType = new();
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private KeyHintViewer viewer;
        private KeyHintType types;

        private void Awake()
        {
            if (eventChannel != null)
            {
                eventChannel.AddListener<KeyHintEvent>(HandleKeyHintEvent);
            }
        }

        private void OnDestroy()
        {
            if (eventChannel != null)
            {
                eventChannel.RemoveListener<KeyHintEvent>(HandleKeyHintEvent);
            }
        }

        private void HandleKeyHintEvent(KeyHintEvent evt)
        {
            if (evt.IsSubscribe)
                Subscribe(evt.KeyHintType, evt.Owner);
            else
                Unsubscribe(evt.KeyHintType, evt.Owner);
        }

        public void Subscribe(KeyHintType type, object owner)
        {
            if (owner == null) return;

            if (hintType.TryAdd(owner, type))
            {
                HintArrangementAndInvoke();
            }
        }

        public void Unsubscribe(KeyHintType type, object owner)
        {
            if (owner == null) return;

            if (hintType.Remove(owner))
            {
                HintArrangementAndInvoke();
            }
        }

        private void HintArrangementAndInvoke()
        {
            if (hintType.Count == 0)
            {
                types = KeyHintType.None;
                if (viewer != null) viewer.CreateHint(types);
                return;
            }

            KeyHintType combinedType = 0;
            foreach (KeyHintType value in hintType.Values)
            {
                combinedType |= value;
            }

            types = combinedType;
            if (viewer != null)
            {
                viewer.CreateHint(types);
            }
        }
    }

    [Flags]
    public enum KeyHintType
    {
        None = 1 << 0,
        UsingItem = 1 << 1,
        PickUpGun = 1 << 2,
        SellingCar = 1 << 3,
        Interact = 1 << 4,
        RemoveWheel = 1 << 5,
        GoToGarage = 1 << 6,
    }
}
