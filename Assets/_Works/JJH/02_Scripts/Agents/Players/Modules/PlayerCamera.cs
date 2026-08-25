using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Modules
{
    public class PlayerCamera : AbstractModule, IPlayerCamera
    {
        [Header("Objects")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform playerCamera;

        [Header("Camera Value")]
        [SerializeField] private float sensitivity = 100f;
        [SerializeField] private float minVertical = -80f;
        [SerializeField] private float maxVertical = 80f;

        private Player _player;

        private float _horizontal;
        private float _vertical;

        private void Awake()
        {
            _player = player.GetComponent<Player>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            RotateCamera();
        }

        private void RotateCamera()
        {
            Vector2 lookDirection = _player.PlayerInput.LookDirection;
            _horizontal += lookDirection.x * sensitivity * Time.deltaTime;
            _vertical -= lookDirection.y * sensitivity * Time.deltaTime;
            _vertical = Mathf.Clamp(_vertical, minVertical, maxVertical);

            player.rotation = Quaternion.Euler(0f, _horizontal, 0f);
            playerCamera.localRotation = Quaternion.Euler(_vertical, 0f, 0f);
        }

        public void CameraShake()
        {

        }
    }
}