using System.Reflection;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public class PlayerMover : MonoBehaviour, IModule, IMover
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float gravity = -19.6f;
        [SerializeField] private CharacterController controller;

        private float _verticalVelocity;
        private Vector3 _inputDirection;
        private Vector3 _manualDirection;

        private ModuleOwner _owner;
        
        public bool IsGround => controller.isGrounded;
        public bool CanManualMove { get; set; } = true;
        
        public void Initialize(ModuleOwner owner)
        {
            _owner = owner;
        }

        public void SetMovementDirection(Vector2 inputDirection)
        {
            _inputDirection = new Vector3(inputDirection.x, 0, inputDirection.y);
        }

        private void Update()
        {
            ApplyGravity();

            if (CanManualMove)
            {
                _manualDirection = _inputDirection * moveSpeed;
            }

            _manualDirection.y = _verticalVelocity;

            controller.Move(_manualDirection * Time.deltaTime);
        }

        public void StopImmediately(bool horizontal, bool vertical)
        {
            if (horizontal)
                controller.Move(new Vector3(0, _manualDirection.y, 0));
            else
                controller.Move(new Vector3(_manualDirection.x, 0, _manualDirection.z));
        }
        
        private void ApplyGravity()
        {
            if (IsGround && _verticalVelocity < 0)
            {
                _verticalVelocity = -2f; 
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime; 
            }
        }

    }
}