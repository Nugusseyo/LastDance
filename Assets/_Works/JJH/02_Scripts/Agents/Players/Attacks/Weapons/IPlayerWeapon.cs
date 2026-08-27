using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public interface IPlayerWeapon
    {
        Weapon CurrentWeapon { get; }
        GameObject CurrentWeaponObject { get; }

        void PickupWeapon();
        void ClearCurrentWeapon();
    }
}
