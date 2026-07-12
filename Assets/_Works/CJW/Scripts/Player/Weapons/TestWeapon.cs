using UnityEngine;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public class TestWeapon : AbstractPlayerWeapon
    {
        protected override void DisableWeapon()
        {
            
        }

        protected override void EnableWeapon()
        {
            
        }

        public override void UseWeapon()
        {
            Debug.Log("Use Weapon");
            base.UseWeapon();
        }

        public override bool CanUseWeapon()
        {
            return true;
        }
    }
}