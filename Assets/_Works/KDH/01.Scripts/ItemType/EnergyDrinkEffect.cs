using _Works.JJH._02_Scripts.Items;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class EnergyDrinkEffect : MonoBehaviour, IItemEffect
    {
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private UseItemSO itemSO;

        public void Apply()
        {
            eventChannel.RaiseEvent(ItemEvents.SpeedBoostEvent.Init(itemSO.Multiplier, itemSO.Duration));
        }
    }
}
