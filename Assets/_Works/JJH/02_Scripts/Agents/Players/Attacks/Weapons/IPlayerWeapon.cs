using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public interface IPlayerWeapon
    {
        GameObject CurrentWeaponObject { get; }
        WeaponDataSO CurrentWeaponData { get; }

        void PickupWeapon(GameObject weaponObject, WeaponDataSO weaponData);
        void ClearCurrentWeapon();
    }
}
