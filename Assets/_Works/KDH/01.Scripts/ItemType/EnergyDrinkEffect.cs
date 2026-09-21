using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class EnergyDrinkEffect : MonoBehaviour, IItemEffect
    {
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private float speedMultiplier = 1.5f;
        [SerializeField] private float duration = 5f;

        public void Apply()
        {
            eventChannel.RaiseEvent(ItemEvents.SpeedBoostEvent.Init(speedMultiplier, duration));
        }
    }
}
