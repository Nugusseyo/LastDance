using _Works.JJH._02_Scripts.Agents.Modules;
using _Works.Shared.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace _Works.JJH._02_Scripts.Items
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class GrabItem : MonoBehaviour
    {
        [field: SerializeField] public ItemDataSO CurrentItemData { get; private set; }

        [Header("Throw")]
        [SerializeField] private LayerMask groundLayer;

        public UnityEvent UseEvent;

        public bool IsThrown { get; private set; }
        private int _throwDamage;

        private Rigidbody _rigidbody;
        private Collider _collider;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();

            SetKinematicState();
        }

        public void UseItem()
        {
            UseEvent?.Invoke();
        }

        public void SetGrabState()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            _collider.isTrigger = true;

            IsThrown = false;
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

            IsThrown = false;
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

        public void StartThrow(int damage)
        {
            IsThrown = true;
            _throwDamage = damage;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsThrown)
                return;

            if ((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
            {
                IsThrown = false;
                return;
            }

            // 손님처럼 맞은 방향으로 밀려나는 대상. 날아온 방향과 빠르기를 같이 넘긴다.
            IHittable hittable = collision.collider.GetComponentInParent<IHittable>();

            if (hittable != null)
            {
                Vector3 direction = collision.collider.bounds.center - transform.position;
                hittable.TakeHit(new HitInfo(_throwDamage, direction, collision.relativeVelocity.magnitude * 0.5f));
                IsThrown = false;
                return;
            }

            IHealth health = collision.collider.GetComponentInParent<IHealth>();

            if (health == null)
                return;

            health.Damage(_throwDamage);
            IsThrown = false;
        }
    }
}