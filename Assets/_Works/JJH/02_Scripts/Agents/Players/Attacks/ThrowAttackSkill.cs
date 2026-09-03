using _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public class ThrowAttackSkill : AbstractPlayerAttack
    {
        [SerializeField] private float throwForce = 10f;

        public override void Attack()
        {
            if (player.Grab == null || player.Grab.CurrentWeapon == null)
                return;

            GrabItem weapon = player.Grab.CurrentWeapon;
            GameObject weaponObject = player.Grab.CurrentGrabObject;

            if (weaponObject == null)
                return;

            Vector3 throwDirection = player.Camera.CameraTrans.forward;

            weaponObject.transform.SetParent(null);
            weapon.Collider.isTrigger = false;
            weapon.Rigidbody.isKinematic = false;
            weapon.Rigidbody.AddForce(throwDirection * throwForce, ForceMode.Impulse);

            player.Grab.ClearCurrentWeapon();
        }
    }
}