using UnityEngine;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public abstract class AbstractPlayerWeapon : MonoBehaviour, IWeapon
    {
        [field: SerializeField] public WeaponDataSO WeaponData { get; private set; }

        public float NormalizedCooldown => Mathf.Approximately(WeaponData.Cooldown, 0f) ? 1f:
            Mathf.Clamp01((Time.time - _lastUseTime) / WeaponData.Cooldown);
        public bool IsUsing { get; private set; }
        
        protected IWeaponModule _weaponModule;
        private float _lastUseTime;
        
        public virtual void InitializeWeapon(IWeaponModule weaponModule)
        {
            _weaponModule = weaponModule;
            weaponModule.OnWeaponChanged += HandleWeaponEnable;
        }

        private void HandleWeaponEnable(IWeapon targetWeapon, bool enable)
        {
            if (targetWeapon == this)
            {
                if (enable)
                    EnableWeapon();
                else
                    DisableWeapon();
            }
        }

        protected abstract void DisableWeapon();
        protected abstract void EnableWeapon();

        public abstract bool CanUseWeapon();

        public virtual void UseWeapon()
        {
            _lastUseTime = Time.time;
            IsUsing = true; 
        }
    }
}