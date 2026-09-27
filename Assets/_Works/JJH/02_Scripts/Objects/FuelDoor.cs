using System;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Objects
{
    public class FuelDoor : MonoBehaviour
    {
        public bool IsFueling { get; private set; }

        public event Action OnFuelingStarted;
        public event Action OnFuelingEnded;

        public void NotifyFuelingStarted()
        {
            if (IsFueling)
                return;

            IsFueling = true;
            OnFuelingStarted?.Invoke();
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