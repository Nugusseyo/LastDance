using _Works.JJH._02_Scripts.Items;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class CiderEffect : MonoBehaviour, IItemEffect
    {
        [SerializeField] private UseItemSO itemSO;
        [SerializeField] private EventChannelSO eventChannelSO;
        [SerializeField] private float duration = 20f;

        public void Apply()
        {
            eventChannelSO.RaiseEvent(UIEvents.ReviewBlockEvent.Init(duration));
            
            #if UNITY_EDITOR
            Debug.Log("Cider Effect Apply");
            #endif
        }
    }
}
