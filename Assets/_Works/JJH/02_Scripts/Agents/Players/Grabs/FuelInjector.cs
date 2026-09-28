using System;
using _Works.JJH._02_Scripts.Objects;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs
{
    public class FuelInjector : MonoBehaviour
    {
        [Header("Layer")]
        [SerializeField] private LayerMask fuelDoorLayerMask;

        [Header("Fuel")]
        [SerializeField] private float fuelDuration = 5f;

        private float originDuration;

        [Header("Duration / UI")]
        [SerializeField] private EventChannelSO durationChannel;
        [SerializeField] private EventChannelSO upgradeChannel;
        private const int UpgradeIndex = 1;

        [Header("Sound")]
        [SerializeField] private EventChannelSO soundChannel;
        [SerializeField] private SoundClipSo fuelingSfx;
        [SerializeField] private int fuelSoundChannelNumber = 20;

        public LayerMask FuelDoorLayerMask => fuelDoorLayerMask;
        public bool IsFueling => _currentDoor != null;

        private FuelDoor _currentDoor;
        private float _holdTime;
        private bool _progressShown;
        private bool _isFuelSoundPlaying;

        private void Awake()
        {
            if(upgradeChannel != null)
                upgradeChannel.AddListener<UpgradeItem>(HandleUpgradeItem);
            originDuration = fuelDuration;
        }

        private void HandleUpgradeItem(UpgradeItem evt)
        {
            if (evt.Index != UpgradeIndex) return;
            float value = originDuration - evt.Item.value.Value * originDuration; //%단위이다.
            fuelDuration = (float)Math.Round(value, 2);
        }

        private void OnDestroy()
        {
            if(upgradeChannel != null)
                upgradeChannel.RemoveListener<UpgradeItem>(HandleUpgradeItem);
        }

        public bool TryStartFueling(FuelDoor door)
        {
            if (door == null)
                return false;

            if (_currentDoor == door)
                return true;

            if (_currentDoor != null)
                CancelFueling();

            _currentDoor = door;
            _holdTime = 0f;

            _currentDoor.NotifyFuelingStarted();
            PlayFuelSound(door.transform);

            return true;
        }

        public void TickFueling(float deltaTime)
        {
            if (_currentDoor == null)
                return;

            _holdTime = Mathf.Min(_holdTime + deltaTime, fuelDuration);
            Debug.Log("<color=green> 주유중 </color>");

            ShowProgress(_holdTime, fuelDuration);

            if (_holdTime >= fuelDuration)
                CompleteFueling();
        }

        private void CompleteFueling()
        {
            FuelDoor door = _currentDoor;

            _currentDoor = null;
            _holdTime = 0f;

            HideProgress();
            StopFuelSound();

            door.NotifyFuelingCompleted();
        }

        public void CancelFueling()
        {
            if (_currentDoor == null)
                return;

            FuelDoor door = _currentDoor;

            _currentDoor = null;
            _holdTime = 0f;

            HideProgress();
            StopFuelSound();

            door.NotifyFuelingEnded();
        }

        private void ShowProgress(float current, float max)
        {
            _progressShown = true;
            if (durationChannel == null) return;
            durationChannel.RaiseEvent(UIEvents.DurationEvent.Init(current, max));
        }

        private void HideProgress()
        {
            if (!_progressShown) return;
            _progressShown = false;
            if (durationChannel == null) return;
            durationChannel.RaiseEvent(UIEvents.DurationEvent.Init(1f, 1f));
        }

        private void PlayFuelSound(Transform doorTransform)
        {
            if (_isFuelSoundPlaying || soundChannel == null || fuelingSfx == null)
                return;

            soundChannel.RaiseEvent(SoundEvents.PlaySoundEvent.Init(doorTransform.position, fuelingSfx,
                                                                                                            fuelSoundChannelNumber, doorTransform));
            _isFuelSoundPlaying = true;
        }

        private void StopFuelSound()
        {
            if (!_isFuelSoundPlaying || soundChannel == null)
                return;

            soundChannel.RaiseEvent(SoundEvents.StopSoundEvent.Init(fuelSoundChannelNumber));
            _isFuelSoundPlaying = false;
        }
    }
}