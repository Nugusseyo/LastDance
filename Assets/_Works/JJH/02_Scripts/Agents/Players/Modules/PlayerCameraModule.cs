using _Works.JYG._Scripts.UI.Setting; // SettingSO 네임스페이스
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Modules
{
    public class PlayerCameraModule : AbstractModule, IPlayerCamera
    {
        [Header("Settings SO")]
        [SerializeField] private SettingSO settingSO;
        [SerializeField] private float sensitivityMultiplier = 2f; // SO의 1~100 수치를 실제 조작감에 맞춰 스케일링할 가중치

        [Header("Objects")]
        [field: SerializeField] public Transform CameraTrans { get; private set; }

        [Header("Camera Value")]
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
        
        // 현재 적용 중인 실제 카메라 감도 수치
        private float _currentSensitivity = 100f;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _player = (Player)_owner;

            InitSensitivity();
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제 (메모리 누수 방지)
            if (settingSO != null)
            {
                settingSO.OnSensitivityChanged -= HandleSensitivityChanged;
            }
        }

        private void InitSensitivity()
        {
            if (settingSO != null)
            {
                settingSO.Load();
                HandleSensitivityChanged(settingSO.mouseSensitivity);

                // SO 변경 이벤트 구독
                settingSO.OnSensitivityChanged += HandleSensitivityChanged;
            }
        }

        private void HandleSensitivityChanged(float newSensitivity)
        {
            // SettingSO의 mouseSensitivity(1~100)에 가중치를 곱해 적용
            _currentSensitivity = newSensitivity * sensitivityMultiplier;
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
            
            // _currentSensitivity 적용
            _horizontal += lookDirection.x * _currentSensitivity * Time.deltaTime;
            _vertical -= lookDirection.y * _currentSensitivity * Time.deltaTime;
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