using _Works.JJH._02_Scripts.Agents.Modules;
using _Works.Shared.Combat;
using System.Collections;
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

        [Header("Hold Offset")]
        [SerializeField] private Vector3 holdPositionOffset;
        [SerializeField] private Vector3 holdRotationOffset;
        [SerializeField] private Vector3 holdScale = Vector3.one;

        [Header("Release")]
        [SerializeField] private float minIgnoreTime = 0.2f;   // 최소 충돌 무시 시간
        [SerializeField] private float maxIgnoreTime = 2f;     // 겹침이 안 풀려도 이 시간 뒤엔 복구
        [SerializeField] private float maxDepenetrationVelocity = 1f;

        public UnityEvent UseEvent;

        public Vector3 HoldPositionOffset => holdPositionOffset;
        public Quaternion HoldRotationOffset => Quaternion.Euler(holdRotationOffset);
        public Vector3 HoldScale => holdScale;

        public bool IsThrown { get; private set; }
        private int _throwDamage;

        private Rigidbody _rigidbody;
        private Collider[] _colliders;

        private Vector3 _originalLocalScale;
        private bool _hasStoredScale;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);

            _rigidbody.maxDepenetrationVelocity = maxDepenetrationVelocity;

            SetKinematicState();
        }

        private void SetTrigger(bool isTrigger)
        {
            foreach (Collider col in _colliders)
            {
                if (col != null)
                    col.isTrigger = isTrigger;
            }
        }

        public void UseItem()
        {
            UseEvent?.Invoke();
        }

        public void SetGrabState()
        {
            if (!_hasStoredScale)
            {
                _originalLocalScale = transform.localScale;
                _hasStoredScale = true;
            }

            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            SetTrigger(true);

            IsThrown = false;
        }

        public void SetPhysicsState()
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
            SetTrigger(false);
        }

        public void SetKinematicState()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            SetTrigger(false);

            IsThrown = false;
        }

        // 겹친 콜라이더들과 충돌을 무시한 채 물리를 켠다. 겹침이 풀리면 충돌을 복구한다.
        public void ReleaseIgnoring(Collider[] ignoreColliders)
        {
            SetPhysicsState();

            if (ignoreColliders == null || ignoreColliders.Length == 0)
                return;

            StartCoroutine(IgnoreCollisionRoutine(ignoreColliders));
        }

        private IEnumerator IgnoreCollisionRoutine(Collider[] others)
        {
            SetIgnore(others, true);

            float elapsed = 0f;
            while (elapsed < maxIgnoreTime)
            {
                elapsed += Time.deltaTime;

                if (elapsed >= minIgnoreTime && !IsOverlapping(others))
                    break;

                yield return null;
            }

            SetIgnore(others, false);
        }

        private bool IsOverlapping(Collider[] others)
        {
            foreach (Collider mine in _colliders)
            {
                if (mine == null) continue;

                foreach (Collider other in others)
                {
                    if (other == null) continue;
                    if (mine.bounds.Intersects(other.bounds))
                        return true;
                }
            }

            return false;
        }

        private void SetIgnore(Collider[] others, bool ignore)
        {
            foreach (Collider mine in _colliders)
            {
                if (mine == null) continue;

                foreach (Collider other in others)
                {
                    if (other == null) continue;
                    Physics.IgnoreCollision(mine, other, ignore);
                }
            }
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

        public void RestoreOriginalScale()
        {
            if (!_hasStoredScale)
                return;

            transform.localScale = _originalLocalScale;
            _hasStoredScale = false;
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

        [ContextMenu("Capture Current Transform As Hold Offset")]
        private void CaptureCurrentTransformAsHoldOffset()
        {
            holdPositionOffset = transform.localPosition;
            holdRotationOffset = transform.localRotation.eulerAngles;
            holdScale = transform.localScale;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}