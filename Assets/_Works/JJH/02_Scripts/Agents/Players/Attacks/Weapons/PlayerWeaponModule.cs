using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public class PlayerWeaponModule : AbstractModule, IPlayerWeapon
    {
        public Weapon CurrentWeapon { get; private set; }
        public GameObject CurrentWeaponObject { get; private set; }

        [SerializeField] private LayerMask weaponLayer;
        [SerializeField] private Transform weaponHoldPoint;

        private Player _player;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _player = (Player)_owner;
        }

        public void PickupWeapon()
        {
            if (_player.Sensor.FindWeapon(_player.Camera.CameraTrans,
                weaponLayer, 5, out Collider weaponCollider) == false)
                return;

            Weapon findWeapon = weaponCollider.GetComponent<Weapon>();

            if (CurrentWeaponObject == null)
            {
                EquipWeapon(findWeapon);
                return;
            }

            SwapWeapon(findWeapon);
        }

        private void EquipWeapon(Weapon findWeapon)
        {
            CurrentWeapon = findWeapon;
            CurrentWeaponObject = findWeapon.gameObject;

            CurrentWeapon.Rigidbody.isKinematic = true;
            CurrentWeapon.Collider.isTrigger = true;

            CurrentWeaponObject.transform.SetParent(weaponHoldPoint);
            CurrentWeaponObject.transform.SetLocalPositionAndRotation(
                                                                    Vector3.zero, Quaternion.identity);
        }

        private void SwapWeapon(Weapon findWeapon)
        {
            Transform currentWeaponTransform = CurrentWeaponObject.transform;
            Transform newWeaponTransform = findWeapon.transform;

            Vector3 newWeaponPosition = newWeaponTransform.position;
            Quaternion newWeaponRotation = newWeaponTransform.rotation;

            currentWeaponTransform.SetParent(null);
            currentWeaponTransform.SetPositionAndRotation(
                newWeaponPosition, newWeaponRotation);

            CurrentWeapon.Rigidbody.isKinematic = false;
            CurrentWeapon.Collider.isTrigger = false;
            newWeaponTransform.SetParent(weaponHoldPoint);
            newWeaponTransform.SetLocalPositionAndRotation(
                Vector3.zero, Quaternion.identity);

            CurrentWeapon = findWeapon;
            CurrentWeaponObject = findWeapon.gameObject;
            CurrentWeapon.Rigidbody.isKinematic = true;
            CurrentWeapon.Collider.isTrigger = true;
        }

        public void ClearCurrentWeapon()
        {
            if (CurrentWeapon != null)
            {
                CurrentWeapon.Rigidbody.isKinematic = false;
                CurrentWeapon.Collider.isTrigger = false;
            }

            CurrentWeapon = null;
            CurrentWeaponObject = null;
        }
    }
}