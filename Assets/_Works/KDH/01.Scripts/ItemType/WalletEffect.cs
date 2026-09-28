using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class WalletEffect : MonoBehaviour, IItemEffect
    {
        [SerializeField] private EventChannelSO eventChannelSO;
        [SerializeField] private float duration = 30f;

        public void Apply()
        {
            eventChannelSO.RaiseEvent(UIEvents.BuffEvent.Init(BuffType.Wallet, duration));

#if UNITY_EDITOR
            Debug.Log("Wallet Effect Apply");
#endif
        }
    }
}
