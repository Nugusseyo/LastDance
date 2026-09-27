using _Works.JJH._02_Scripts.Items;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class ColaEffect : MonoBehaviour, IItemEffect
    {
        [SerializeField] private UseItemSO itemSO;
        [SerializeField] private EventChannelSO eventChannelSO;
        [SerializeField] private float duration = 30f;
        
        public void Apply()
        {
            eventChannelSO.RaiseEvent(UIEvents.ReviewBuffEvent.Init(duration));

#if UNITY_EDITOR
            Debug.Log("Cola Effect Apply");
#endif
        }
    }
}
