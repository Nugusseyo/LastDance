using System;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;
using UnityEngine.EventSystems;

namespace _Works.JYG._Scripts.Vending_Machine
{
    public class VendingTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        private VendingTooltipData _tooltipData;
        [SerializeField] private EventChannelSO eventChannel;
        public void TooltipInit(VendingTooltipData data)
        {
            _tooltipData = data;
        }
        
        

        public void InvokeEventChannel(bool active)
        {
            if (eventChannel != null)
                eventChannel.RaiseEvent(UIEvents.TooltipEvent.
                    Init(_tooltipData.itemName, _tooltipData.tip, active));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            InvokeEventChannel(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            InvokeEventChannel(false);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (eventChannel != null)
                eventChannel.RaiseEvent(UIEvents.TipMoveEvent.Init(eventData.position));
        }
    }
}
