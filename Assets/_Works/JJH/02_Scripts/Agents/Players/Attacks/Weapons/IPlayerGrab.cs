using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public interface IPlayerGrab
    {
        GrabItem CurrentWeapon { get; }
        GameObject CurrentGrabObject { get; }

        void PickupWeapon();
        void ClearCurrentWeapon();
    }
}
