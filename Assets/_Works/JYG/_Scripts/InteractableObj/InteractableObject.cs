using System;
using UnityEngine;

namespace _Works.JYG._Scripts.InteractableObj
{
    public class InteractableObject : MonoBehaviour
    {
        public event Action OnInteract;
        public event Action OnDisconnect;

        private IInteractableObject[] _interactableObjects;

        private void Awake()
        {
            _interactableObjects = GetComponentsInChildren<IInteractableObject>();

            if (_interactableObjects == null || _interactableObjects.Length <= 0) return;
            
            foreach (IInteractableObject obj in _interactableObjects)
            {
                if (obj == null) continue;
                
                OnInteract += obj.HandleInteract;
                OnDisconnect += obj.HandleDisconnect;
            }
        }

        private void OnDestroy()
        {
            if (_interactableObjects == null || _interactableObjects.Length <= 0) return;
            
            foreach (IInteractableObject obj in _interactableObjects)
            {
                if (obj == null) continue;
                
                OnInteract -= obj.HandleInteract;
                OnDisconnect -= obj.HandleDisconnect;
            }
        }

        public void HandleOnInteract() => OnInteract?.Invoke();
        public void HandleOnDisconnect() => OnDisconnect?.Invoke();
    }
}
