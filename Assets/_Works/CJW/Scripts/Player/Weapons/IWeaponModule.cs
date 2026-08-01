using System;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public interface IWeaponModule
    {
        event Action<IWeapon, bool> OnWeaponChanged;
        IWeapon CurrentWeapon { get; }
        bool IsUsingWeapon { get; }
        void UseWeapon();
        void ChangeWeapon(int scrollValue);
    }
}