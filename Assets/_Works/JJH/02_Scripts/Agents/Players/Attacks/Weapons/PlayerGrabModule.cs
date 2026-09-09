using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public class PlayerGrabModule : AbstractModule, IPlayerGrab
    {
        public GrabItem CurrentWeapon { get; private set; }
        public GameObject CurrentGrabObject { get; private set; }

        [Header("Layer")]
        [SerializeField] private LayerMask weaponLayer;

        [Header("Grab")]
        [SerializeField] private Transform weaponHoldPoint;
        [SerializeField] private float pickupDistance = 4f;

        [Header("Car")]
        [SerializeField] private PartDetacher partDetacher;

        private Player _player;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _player = (Player)owner;
        }

        public void PickupItem()
        {
            if (CurrentGrabObject != null)
            {
                if (AttachCurrentItem())
                    return;

                DropItem();
                return;
            }

            if (partDetacher != null
                && _player.Sensor.FindItem(_player.Camera.CameraTrans, partDetacher.PartLayerMask,
                                                                pickupDistance, out Collider partCollider))
            {
                GrabItem part = partCollider.GetComponent<GrabItem>();

                if (part == null)
                    return;

                if (part.transform.parent != null
                    && !partDetacher.TryDetachPart(part))
                    return;

                EquipItem(part);
                return;
            }

            if (!_player.Sensor.FindItem(_player.Camera.CameraTrans, weaponLayer, pickupDistance, out Collider collider))
                return;

            GrabItem item = collider.GetComponent<GrabItem>();

            if (item == null)
                return;

            EquipItem(item);
        }

        private void EquipItem(GrabItem item)
        {
            if (item == null)
                return;

            CurrentWeapon = item;
            CurrentGrabObject = item.gameObject;

            item.SetGrabState();

            CurrentGrabObject.transform.SetParent(weaponHoldPoint, true);
            CurrentGrabObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void SwapItem(GrabItem item)
        {
            if (item == null || item == CurrentWeapon)
                return;

            if (CurrentGrabObject == null)
            {
                EquipItem(item);
                return;
            }

            Transform currentItemTransform = CurrentGrabObject.transform;
            Vector3 itemPosition = item.transform.position;
            Quaternion itemRotation = item.transform.rotation;

            currentItemTransform.SetParent(null);
            currentItemTransform.SetPositionAndRotation(itemPosition, itemRotation);

            CurrentWeapon.SetPhysicsState();

            EquipItem(item);
        }

        public void DropItem()
        {
            if (CurrentWeapon == null)
                return;

            CurrentWeapon.transform.SetParent(null);
            CurrentWeapon.SetPhysicsState();

            CurrentWeapon = null;
            CurrentGrabObject = null;
        }

        public void ClearCurrentItem()
        {
            if (CurrentWeapon != null)
                CurrentWeapon.SetPhysicsState();

            CurrentWeapon = null;
            CurrentGrabObject = null;
        }

        public bool AttachCurrentItem()
        {
            if (CurrentWeapon == null || partDetacher == null)
                return false;

            if (!partDetacher.TryAttachWheel(CurrentWeapon, _player.Camera.CameraTrans))
                return false;

            CurrentWeapon = null;
            CurrentGrabObject = null;

            return true;
        }
    }
}