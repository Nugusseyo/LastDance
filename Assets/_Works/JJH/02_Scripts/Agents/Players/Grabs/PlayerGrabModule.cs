using _Works.JJH._02_Scripts.Items;
using _Works.JJH._02_Scripts.Objects;
using _Works.KDH._01.Scripts.Car;
using _Works.KDH._01.Scripts.Wrench;
using DevLib.EventChannelSystem;
using DevLib.ModuleSystem;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs
{
    public class PlayerGrabModule : AbstractModule, IPlayerGrab
    {
        public GrabItem CurrentItem { get; private set; }
        public GameObject CurrentGrabObject { get; private set; }

        [Header("Layer")]
        [SerializeField] private LayerMask itemLayer;
        [SerializeField] private LayerMask gasStationLayer;

        [Header("Grab")]
        [SerializeField] private Transform weaponHoldPoint;
        [SerializeField] private float pickupDistance = 4f;

        [Header("Car")]
        [SerializeField] private PartDetacher partDetacher;

        [Header("Fuel")]
        [SerializeField] private FuelInjector fuelInjector;

        [Header("Sound")]
        [SerializeField] private EventChannelSO soundChannel;
        [SerializeField] private SoundClipSo drinkSfx;

        private Player _player;

        private float _currentWeaponSpeedMultiplier = 1f;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _player = (Player)owner;
        }

        private void Update()
        {
            if (_player == null)
                return;

            if (!ReferenceEquals(CurrentItem, null) && CurrentItem == null)
                ClearCurrentItem();

            UpdateDetachHold();
            UpdateFuelHold();
        }

        public void UseItem()
        {
            if (CurrentItem == null)
                return;

            CurrentItem.UseItem();

            soundChannel.RaiseEvent(SoundEvents.PlaySoundEvent.Init(transform.position, drinkSfx, 0, null));
        }

        public void PickupItem()
        {
            bool holdingWrench = CurrentGrabObject != null && CurrentGrabObject.GetComponent<WrenchTool>() != null;

            if (CurrentGrabObject != null)
            {
                if (CurrentItem is FuelNozzle && fuelInjector != null
                  && _player.Sensor.FindItem(_player.Camera.CameraTrans, fuelInjector.FuelDoorLayerMask,
                                                                  pickupDistance, out Collider fuelDoorCollider))
                {
                    FuelDoor fuelDoor = fuelDoorCollider.GetComponent<FuelDoor>();

                    if (fuelDoor != null && fuelInjector.TryStartFueling(fuelDoor))
                        return;
                }

                if (holdingWrench && partDetacher != null
                  && _player.Sensor.FindItem(_player.Camera.CameraTrans, partDetacher.PartLayerMask,
                                                                  pickupDistance, out Collider wrenchPartCollider))
                {
                    GrabItem wrenchTargetPart = wrenchPartCollider.GetComponent<GrabItem>();

                    if (wrenchTargetPart != null)
                    {
                        partDetacher.TryDetachPart(wrenchTargetPart);
                        return;
                    }
                }

                if (AttachCurrentItem())
                    return;

                DropItem();
                return;
            }

            // 주유구는 뒷바퀴 바로 위라 바퀴(부품)와 겹친다. 두 레이어를 한 번에 쏴서 실제로 조준한 쪽을 고른다.
            LayerMask fuelDoorMask = fuelInjector != null ? fuelInjector.FuelDoorLayerMask : (LayerMask)0;
            if (fuelInjector != null && partDetacher != null
              && _player.Sensor.FindItem(_player.Camera.CameraTrans, partDetacher.PartLayerMask | fuelDoorMask,
                                                              pickupDistance, out Collider aimedCollider)
              && (fuelDoorMask.value & (1 << aimedCollider.gameObject.layer)) != 0)
            {
                FuelDoor aimedDoor = aimedCollider.GetComponent<FuelDoor>();

                if (aimedDoor != null)
                {
                    fuelInjector.TryStartFueling(aimedDoor);
                    return;
                }
            }

            if (partDetacher != null
              && _player.Sensor.FindItem(_player.Camera.CameraTrans, partDetacher.PartLayerMask,
                                                              pickupDistance, out Collider partCollider))
            {
                GrabItem part = partCollider.GetComponent<GrabItem>();

                if (part == null)
                    return;

                if (!partDetacher.TryDetachPart(part))
                    return;

                EquipItem(part);
                return;
            }

            if (_player.Sensor.FindItem(_player.Camera.CameraTrans, gasStationLayer, pickupDistance, out Collider stationCollider))
            {
                GasStationController station = stationCollider.GetComponent<GasStationController>();

                if (station == null)
                    station = stationCollider.GetComponentInParent<GasStationController>();

                if (station == null)
                    return;

                FuelNozzle nozzle = station.TryDetachNozzle();

                if (nozzle == null)
                    return;

                EquipItem(nozzle);
                return;
            }

            if (fuelInjector != null
              && _player.Sensor.FindItem(_player.Camera.CameraTrans, fuelInjector.FuelDoorLayerMask,
                                                              pickupDistance, out Collider fuelDoorCollider2))
            {
                FuelDoor fuelDoor = fuelDoorCollider2.GetComponent<FuelDoor>();

                if (fuelDoor == null)
                    return;

                fuelInjector.TryStartFueling(fuelDoor);
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
            CurrentGrabObject.transform.SetLocalPositionAndRotation(item.HoldPositionOffset, item.HoldRotationOffset);
            CurrentGrabObject.transform.localScale = item.HoldScale;

            ApplyWeaponSpeedModifier(item);
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

            CurrentItem.RestoreOriginalScale();
            CurrentItem.SetPhysicsState();

            EquipItem(item);
        }

        public void DropItem()
        {
            if (CurrentItem == null)
                return;

            CurrentItem.transform.SetParent(null);
            CurrentItem.RestoreOriginalScale();
            CurrentItem.SetPhysicsState();

            CurrentItem = null;
            CurrentGrabObject = null;

            ResetWeaponSpeedModifier();
        }

        public void ClearCurrentItem()
        {
            if (CurrentItem != null)
            {
                CurrentItem.RestoreOriginalScale();
                CurrentItem.SetPhysicsState();
            }

            CurrentItem = null;
            CurrentGrabObject = null;

            ResetWeaponSpeedModifier();
        }

        public bool AttachCurrentItem()
        {
            if (CurrentItem == null)
                return false;

            if (CurrentItem is FuelNozzle nozzle)
            {
                if (nozzle.Station == null)
                    return false;

                if (!_player.Sensor.FindItem(_player.Camera.CameraTrans, gasStationLayer,
                        pickupDistance, out Collider stationCollider))
                    return false;

                GasStationController station = stationCollider.GetComponent<GasStationController>();
                if (station == null)
                    station = stationCollider.GetComponentInParent<GasStationController>();

                if (station == null || station != nozzle.Station)
                    return false;

                if (!station.TryAttachNozzle(nozzle))
                    return false;

                CurrentItem.RestoreOriginalScale();
                CurrentItem = null;
                CurrentGrabObject = null;
                ResetWeaponSpeedModifier();

                return true;
            }

            if (partDetacher == null)
                return false;

            if (!partDetacher.TryAttachWheel(CurrentItem, _player.Camera.CameraTrans))
                return false;

            CurrentItem.RestoreOriginalScale();
            CurrentItem = null;
            CurrentGrabObject = null;
            ResetWeaponSpeedModifier();

            return true;
        }

        public void DestroyCurrentItem()
        {
            if (CurrentGrabObject == null)
                return;

            Destroy(CurrentGrabObject);

            ClearCurrentItem();
        }

        public void UpdateDetachHold()
        {
            if (partDetacher == null || !partDetacher.IsDetaching)
                return;

            WrenchTool wrench = CurrentGrabObject != null
                                                ? CurrentGrabObject.GetComponent<WrenchTool>()
                                                : null;

            if (CurrentGrabObject != null && wrench == null)
            {
                partDetacher.CancelDetach();
                return;
            }

            bool holding = _player.PlayerInput != null && _player.PlayerInput.IsInteractHeld;

            if (!holding)
            {
                partDetacher.CancelDetach();
                return;
            }

            partDetacher.TickDetach(Time.deltaTime, wrench);
        }

        public void UpdateFuelHold()
        {
            if (fuelInjector == null || !fuelInjector.IsFueling)
                return;

            bool holding = _player.PlayerInput != null && _player.PlayerInput.IsInteractHeld;

            if (!holding || !(CurrentItem is FuelNozzle))
            {
                fuelInjector.CancelFueling();
                return;
            }

            fuelInjector.TickFueling(Time.deltaTime);
        }

        private void ApplyWeaponSpeedModifier(GrabItem item)
        {
            if (_player.Mover == null)
                return;

            float multiplier = item.CurrentItemData is WeaponItemSO weaponData && weaponData.MoveSpeedMultiplier > 0f
                                        ? weaponData.MoveSpeedMultiplier
                                        : 1f;

            if (Mathf.Approximately(multiplier, _currentWeaponSpeedMultiplier))
                return;

            _player.Mover.MoveSpeed = _player.Mover.MoveSpeed / _currentWeaponSpeedMultiplier * multiplier;
            _player.Mover.RunSpeed = _player.Mover.RunSpeed / _currentWeaponSpeedMultiplier * multiplier;

            _currentWeaponSpeedMultiplier = multiplier;
        }

        private void ResetWeaponSpeedModifier()
        {
            if (_player.Mover == null || Mathf.Approximately(_currentWeaponSpeedMultiplier, 1f))
                return;

            _player.Mover.MoveSpeed /= _currentWeaponSpeedMultiplier;
            _player.Mover.RunSpeed /= _currentWeaponSpeedMultiplier;

            _currentWeaponSpeedMultiplier = 1f;
        }
    }
}