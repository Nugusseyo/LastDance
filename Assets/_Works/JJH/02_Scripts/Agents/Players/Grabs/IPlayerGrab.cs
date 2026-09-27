using _Works.JJH._02_Scripts.Items;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs
{
    public interface IPlayerGrab
    {
        GrabItem CurrentItem { get; }
        GameObject CurrentGrabObject { get; }

        void UseItem();
        void PickupItem();
        void ClearCurrentItem();
    }
}
