using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Modules
{
    public class PlayerCameraModule : AbstractModule, IPlayerCamera
    {
        [Header("Objects")]
        [field: SerializeField] public Transform CameraTrans { get; private set; }

        [Header("Camera Value")]
        [SerializeField] private float sensitivity = 100f;
        [SerializeField] private float minVertical = -80f;
        [SerializeField] private float maxVertical = 80f;

        [Header("Camera Shake")]
        [SerializeField] private float shakeSpeed = 10f;
        [SerializeField] private float shakeAmount = 1.5f;

        private Player _player;

        private float _horizontal;
        private float _vertical;

        private float _shakeWeight;
        private bool _isShake = false;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _player = (Player)_owner;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            RotateCamera();
            if (_isShake)
                ShakeCamera();
        }

        private void RotateCamera()
        {
            Vector2 lookDirection = _player.PlayerInput.LookDirection;
            _horizontal += lookDirection.x * sensitivity * Time.deltaTime;
            _vertical -= lookDirection.y * sensitivity * Time.deltaTime;
            _vertical = Mathf.Clamp(_vertical, minVertical, maxVertical);

            _player.transform.rotation = Quaternion.Euler(0f, _horizontal, 0f);
            CameraTrans.localRotation = Quaternion.Euler(_vertical, 0f, 0f);
        }

        private void ShakeCamera()
        {
            _shakeWeight = Mathf.MoveTowards(_shakeWeight, 1f, Time.deltaTime * 5f);

            float shake = Mathf.Sin(Time.time * shakeSpeed) * shakeAmount * _shakeWeight;
            CameraTrans.localRotation = Quaternion.Euler(_vertical + shake, 0f, 0f);
        }

        public void SetCameraShake(bool isRunning)
        {
            _isShake = isRunning;
            if (isRunning)
            {
                _shakeWeight = 0f;
            }
            else
            {
                _shakeWeight = 0f;
                CameraTrans.localRotation = Quaternion.Euler(_vertical, 0f, 0f);
            }
        }
    }
}