using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public class PlayerGrabModule : AbstractModule, IPlayerGrab
    {
        public GrabItem CurrentWeapon { get; private set; }
        public GameObject CurrentGrabObject { get; private set; }

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

            GrabItem findWeapon = weaponCollider.GetComponent<GrabItem>();

            if (CurrentGrabObject == null)
            {
                EquipWeapon(findWeapon);
                return;
            }

            SwapWeapon(findWeapon);
        }

        private void EquipWeapon(GrabItem findWeapon)
        {
            CurrentWeapon = findWeapon;
            CurrentGrabObject = findWeapon.gameObject;

            CurrentWeapon.Rigidbody.isKinematic = true;
            CurrentWeapon.Collider.isTrigger = true;

            CurrentGrabObject.transform.SetParent(weaponHoldPoint, true);
            CurrentGrabObject.transform.SetLocalPositionAndRotation(
                                                                    Vector3.zero, Quaternion.identity);
        }

        private void SwapWeapon(GrabItem findWeapon)
        {
            Transform currentWeaponTransform = CurrentGrabObject.transform;
            Transform newWeaponTransform = findWeapon.transform;

            Vector3 newWeaponPosition = newWeaponTransform.position;
            Quaternion newWeaponRotation = newWeaponTransform.rotation;

            currentWeaponTransform.SetParent(null);
            currentWeaponTransform.SetPositionAndRotation(
                newWeaponPosition, newWeaponRotation);

            CurrentWeapon.Rigidbody.isKinematic = false;
            CurrentWeapon.Collider.isTrigger = false;
            newWeaponTransform.SetParent(weaponHoldPoint, true);
            newWeaponTransform.SetLocalPositionAndRotation(
                Vector3.zero, Quaternion.identity);

            CurrentWeapon = findWeapon;
            CurrentGrabObject = findWeapon.gameObject;
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
            CurrentGrabObject = null;
        }
    }
}