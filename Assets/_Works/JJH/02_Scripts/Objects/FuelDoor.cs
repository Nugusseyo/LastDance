using System;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Objects
{
    public class FuelDoor : MonoBehaviour
    {
        public bool IsFueling { get; private set; }

        public event Action OnFuelingStarted;
        public event Action OnFuelingEnded;

        /// <summary>게이지를 끝까지 채워 주유를 마쳤다. 중간에 손을 떼면 오지 않고 OnFuelingEnded만 온다.</summary>
        public event Action OnFuelingCompleted;

        public void NotifyFuelingStarted()
        {
            if (IsFueling)
                return;

            IsFueling = true;
            OnFuelingStarted?.Invoke();
        }

        public void NotifyFuelingCompleted()
        {
            if (!IsFueling)
                return;

            OnFuelingCompleted?.Invoke();
            NotifyFuelingEnded();
        }

        public void NotifyFuelingEnded()
        {
            if (!IsFueling)
                return;

            IsFueling = false;
            OnFuelingEnded?.Invoke();
        }
    }
}