using System;
using UnityEngine;
using UnityEngine.Events;

namespace _Works.JYG._Scripts.InteractableObj
{
    public class InteractableObject : MonoBehaviour
    {
        public UnityEvent OnInteract;
        public UnityEvent OnDisconnect;

        [SerializeField] private Vector3 areaSize = new Vector3(5f, 5f, 5f); 
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, 0f);
        [SerializeField] private LayerMask whatIsTarget;

        private readonly Collider[] playerColliders = new Collider[3];
        private bool isConnected = false;

        [SerializeField] private bool onDisconnectAwake = true;

        private void Awake()
        {
            if (onDisconnectAwake)
                OnDisconnect?.Invoke();
        }

        private void FixedUpdate()
        {
            int count =
                Physics.OverlapBoxNonAlloc(transform.position + offset, areaSize / 2, playerColliders, Quaternion.identity, whatIsTarget);

            if (count > 0 && !isConnected)
            {
                isConnected = true;
                OnInteract.Invoke();
            }
            else if (count <= 0 && isConnected)
            {
                isConnected = false;
                OnDisconnect.Invoke();
            }
        }

        public void HandleOnInteract() => OnInteract?.Invoke();
        public void HandleOnDisconnect() => OnDisconnect?.Invoke();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + offset, areaSize);
        }
    }
}
