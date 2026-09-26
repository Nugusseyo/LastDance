using _Works.JJH._02_Scripts.Items;
using _Works.KDH._01.Scripts.Car;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs
{
    public class PlayerGrabModule : AbstractModule, IPlayerGrab
    {
        public GrabItem CurrentItem { get; private set; }
        public GameObject CurrentGrabObject { get; private set; }

        [Header("Layer")]
        [SerializeField] private LayerMask itemLayer;

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

        public void UseItem()
        {
            CurrentItem.UseItem();
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

            if (!_player.Sensor.FindItem(_player.Camera.CameraTrans, itemLayer, pickupDistance, out Collider collider))
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

            CurrentItem = item;
            CurrentGrabObject = item.gameObject;

            item.SetGrabState();

            CurrentGrabObject.transform.SetParent(weaponHoldPoint, true);
            CurrentGrabObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        public void SwapItem(GrabItem item)
        {
            if (item == null || item == CurrentItem)
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

            CurrentItem.SetPhysicsState();

            EquipItem(item);
        }

        public void DropItem()
        {
            if (CurrentItem == null)
                return;

            CurrentItem.transform.SetParent(null);
            CurrentItem.SetPhysicsState();

            CurrentItem = null;
            CurrentGrabObject = null;
        }

        public void ClearCurrentItem()
        {
            if (CurrentItem != null)
                CurrentItem.SetPhysicsState();

            CurrentItem = null;
            CurrentGrabObject = null;
        }

        public bool AttachCurrentItem()
        {
            if (CurrentItem == null || partDetacher == null)
                return false;

            if (!partDetacher.TryAttachWheel(CurrentItem, _player.Camera.CameraTrans))
                return false;

            CurrentItem = null;
            CurrentGrabObject = null;

            return true;
        }

        public void DestroyCurrentItem()
        {
            if (CurrentGrabObject == null)
                return;

            Destroy(CurrentGrabObject);

            ClearCurrentItem();
        }
    }
}