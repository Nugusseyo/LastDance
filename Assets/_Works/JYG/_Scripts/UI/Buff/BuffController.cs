using System;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.Buff
{
    public class BuffController : MonoBehaviour
    {
        [SerializeField] private EventChannelSO eventChannel;

        private void Awake()
        {
            if (eventChannel != null)
                eventChannel.AddListener<BuffEvent>(HandleBuffApply);
        }
        
        private void OnDestroy()
        {
            if(eventChannel != null)
                eventChannel.RemoveListener<BuffEvent>(HandleBuffApply);
        }
        
        private void HandleBuffApply(BuffEvent evt)
        {
            
        }

    }
}
