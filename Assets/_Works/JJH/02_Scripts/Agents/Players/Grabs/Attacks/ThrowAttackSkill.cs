using _Works.JJH._02_Scripts.Items;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs.Attacks
{
    public class ThrowAttackSkill : AbstractPlayerAttack
    {
        [SerializeField] private float throwForce = 10f;

        public override void Attack()
        {
            if (player.Grab == null || player.Grab.CurrentItem == null)
                return;

            GrabItem weapon = player.Grab.CurrentItem;
            GameObject weaponObject = player.Grab.CurrentGrabObject;

            if (weaponObject == null)
                return;

            Vector3 throwDirection = player.Camera.CameraTrans.forward;

            weaponObject.transform.SetParent(null);
            weapon.SetPhysicsState();
            weapon.AddForce(throwDirection * throwForce, ForceMode.Impulse);

            player.Grab.ClearCurrentItem();
        }
    }
}