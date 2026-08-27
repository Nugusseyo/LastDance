using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public class PlayerWeaponModule : AbstractModule, IPlayerWeapon
    {
        public GameObject CurrentWeaponObject { get; private set; }
        public WeaponDataSO CurrentWeaponData { get; private set; }

        [SerializeField] private Transform weaponHoldPoint;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
        }

        public void PickupWeapon(GameObject weaponObject, WeaponDataSO weaponData)
        {
            if (weaponObject == null)
                return;

            if (weaponData == null)
                return;

            if (CurrentWeaponObject == null)
            {
                EquipWeapon(
                    weaponObject,
                    weaponData);

                return;
            }

            SwapWeapon(weaponObject, weaponData);
        }

        private void EquipWeapon(GameObject weaponObject, WeaponDataSO weaponData)
        {
            CurrentWeaponObject = weaponObject;
            CurrentWeaponData = weaponData;

            weaponObject.transform.SetParent(weaponHoldPoint);
            weaponObject.transform.SetLocalPositionAndRotation(
                Vector3.zero, Quaternion.identity);
        }

        private void SwapWeapon(GameObject newWeaponObject, WeaponDataSO newWeaponData)
        {
            Transform currentWeaponTransform = CurrentWeaponObject.transform;
            Transform newWeaponTransform = newWeaponObject.transform;

            Vector3 newWeaponPosition = newWeaponTransform.position;
            Quaternion newWeaponRotation = newWeaponTransform.rotation;

            currentWeaponTransform.SetParent(null);
            currentWeaponTransform.SetPositionAndRotation(
                newWeaponPosition, newWeaponRotation);

            newWeaponTransform.SetParent(weaponHoldPoint);
            newWeaponTransform.SetLocalPositionAndRotation(
                Vector3.zero, Quaternion.identity);

            CurrentWeaponObject = newWeaponObject;
            CurrentWeaponData = newWeaponData;
        }

        public void ClearCurrentWeapon()
        {
            CurrentWeaponObject = null;
            CurrentWeaponData = null;
        }
    }
}