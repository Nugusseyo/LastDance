using System;
using DevLib.ModuleSystem;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public interface ICleanerModule
    {
        event Action OnCleanEnd;
        event Action<ICleaner, bool> OnWeaponChanged;
        ICleaner CurrentCleaner { get; }
        ModuleOwner Owner { get; }
        bool IsUsingWeapon { get; }
        void UseCleaner();
        void ChangeWeapon(int scrollValue);
    }
}