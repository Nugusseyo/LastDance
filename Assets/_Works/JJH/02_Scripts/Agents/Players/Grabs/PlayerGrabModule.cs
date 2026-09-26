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
            Debug.Log("PickupItem 호출");

            if (CurrentGrabObject != null)
            {
                Debug.Log($"현재 들고 있는 아이템 있음 : {CurrentGrabObject.name}");

                if (AttachCurrentItem())
                {
                    Debug.Log("아이템 장착 성공");
                    return;
                }

                Debug.Log("장착 실패 → DropItem");
                DropItem();
                return;
            }

            if (partDetacher != null
                && _player.Sensor.FindItem(_player.Camera.CameraTrans, partDetacher.PartLayerMask,
                                                                pickupDistance, out Collider partCollider))
            {
                Debug.Log($"차량 파트 감지 : {partCollider.name}");

                GrabItem part = partCollider.GetComponent<GrabItem>();

                if (part == null)
                {
                    Debug.LogWarning($"차량 파트에 GrabItem 없음 : {partCollider.name}");
                    return;
                }

                if (part.transform.parent != null
                    && !partDetacher.TryDetachPart(part))
                {
                    Debug.LogWarning($"차량 파트 분리 실패 : {part.name}");
                    return;
                }

                Debug.Log($"차량 파트 줍기 : {part.name}");
                EquipItem(part);
                return;
            }

            if (!_player.Sensor.FindItem(_player.Camera.CameraTrans, itemLayer, pickupDistance, out Collider collider))
            {
                Debug.Log("아이템 감지 실패");
                return;
            }

            Debug.Log($"아이템 감지 성공 : {collider.name}");

            GrabItem item = collider.GetComponent<GrabItem>();

            if (item == null)
            {
                Debug.LogWarning($"Collider에 GrabItem 없음 : {collider.name}");
                return;
            }

            Debug.Log($"GrabItem 찾음 : {item.name}");
            EquipItem(item);
        }

        private void EquipItem(GrabItem item)
        {
            if (item == null)
            {
                Debug.LogWarning("EquipItem : item이 null");
                return;
            }

            Debug.Log($"EquipItem 실행 : {item.name}");

            CurrentItem = item;
            CurrentGrabObject = item.gameObject;

            item.SetGrabState();

            CurrentGrabObject.transform.SetParent(weaponHoldPoint, true);
            CurrentGrabObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            Debug.Log($"아이템 장착 완료 : {item.name}");
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
    }
}