namespace _Works.CJW.Scripts.Player.Weapons
{
    public interface IWeapon
    {
        WeaponDataSO WeaponData { get; }
        float NormalizedCooldown { get; }
        public bool IsUsing { get; }
        void InitializeWeapon(IWeaponModule weaponModule);
        bool CanUseWeapon();
        void UseWeapon();
    }
}