using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    [RequireComponent(typeof(Rigidbody))]
    public class AgentMover : AbstractModule, IMover
    {
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float runSpeed = 6f;

        private Rigidbody _rigidbody;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _rigidbody = GetComponent<Rigidbody>();
        }

        public void Move(Vector3 direction)
        {
            SetVelocity(direction, moveSpeed);
        }

        public void Run(Vector3 direction)
        {
            SetVelocity(direction, runSpeed);
        }

        public void Stop()
        {
            Vector3 velocity = _rigidbody.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;

            _rigidbody.linearVelocity = velocity;
        }

        private void SetVelocity(Vector3 direction, float speed)
        {
            direction.y = 0f;

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            Vector3 velocity = direction * speed;
            velocity.y = _rigidbody.linearVelocity.y;

            _rigidbody.linearVelocity = velocity;
        }
    }
}