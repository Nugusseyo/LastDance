using System.Reflection;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public class PlayerMover : MonoBehaviour, IModule, IMover
    {
        [Header("Camera Reference")]
        [SerializeField] private Transform cameraTransform;

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

            // cameraTransform이 인스펙터에서 비어있다면 Main Camera 자동 할당
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        public void SetMovementDirection(Vector2 inputDirection)
        {
            _inputDirection = new Vector3(inputDirection.x, 0, inputDirection.y);
        }

        private void Update()
        {
            ApplyGravity();
            RotatePlayerWithCamera(); // 1. 카메라 Y축 기준으로 플레이어 몸통 회전

            if (CanManualMove)
            {
                // 2. 회전된 플레이어 바라보는 방향(Local)을 기준(World)으로 이동 방향 계산
                Vector3 moveWorldDir = transform.TransformDirection(_inputDirection);
                _manualDirection = moveWorldDir * moveSpeed;
            }

            _manualDirection.y = _verticalVelocity;

            controller.Move(_manualDirection * Time.deltaTime);
        }

        /// <summary>
        /// 카메라가 바라보는 Y축(좌우) 회전값을 플레이어 몸통에 동기화
        /// </summary>
        private void RotatePlayerWithCamera()
        {
            if (cameraTransform == null) return;

            Vector3 cameraEuler = cameraTransform.eulerAngles;
            _owner.transform.rotation = Quaternion.Euler(0f, cameraEuler.y, 0f);
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