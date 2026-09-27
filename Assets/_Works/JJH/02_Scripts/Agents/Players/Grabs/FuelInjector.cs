using _Works.JJH._02_Scripts.Objects;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs
{
    public class FuelInjector : MonoBehaviour
    {
        [Header("Layer")]
        [SerializeField] private LayerMask fuelDoorLayerMask;

        [Header("Fuel")]
        [SerializeField] private float fuelDuration = 5f;

        [Header("Duration / UI")]
        [SerializeField] private EventChannelSO durationChannel;

        public LayerMask FuelDoorLayerMask => fuelDoorLayerMask;
        public bool IsFueling => _currentDoor != null;

        private FuelDoor _currentDoor;
        private float _holdTime;
        private bool _progressShown;

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

        /// <summary>게이지가 다 찼다. 주유구에 완료를 알리고 주유를 끝낸다.</summary>
        private void CompleteFueling()
        {
            FuelDoor door = _currentDoor;

            _currentDoor = null;
            _holdTime = 0f;

            HideProgress();

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
    }
}