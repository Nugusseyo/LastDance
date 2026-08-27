using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public class ThrowAttackSkill : AbstractPlayerAttack
    {
        [SerializeField] private float throwForce = 10f;

        public override void Attack()
        {
            if (_weapon == null)
                return;

            GameObject weaponObject = _weapon.CurrentWeaponObject;

            if (weaponObject == null)
                return;

            weaponObject.transform.SetParent(null);
            Rigidbody rigidbody = weaponObject.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.isKinematic = false;
                rigidbody.AddForce(transform.forward * throwForce, ForceMode.Impulse);
            }

            _weapon.ClearCurrentWeapon();
        }
    }
}