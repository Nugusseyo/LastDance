using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class GrabItem : MonoBehaviour
    {
        [field: SerializeField] public ItemDataSO CurrentItemData { get; private set; }

        private Rigidbody _rigidbody;
        private Collider _collider;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
        }

        private void Start()
        {
            SetKinematicState();
        }

        public void SetGrabState()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _collider.isTrigger = true;
        }

        public void SetPhysicsState()
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            _collider.isTrigger = false;
        }

        public void SetKinematicState()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _collider.isTrigger = false;
        }

        public void StopPhysics()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        public void AddForce(Vector3 force, ForceMode forceMode)
        {
            _rigidbody.AddForce(force, forceMode);
        }
    }
}