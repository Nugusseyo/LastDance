using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.CJW.Scripts.Player
{
    [CreateAssetMenu(fileName = "InputReader", menuName = "Input Reader", order = 0)]
    public class InputReader : ScriptableObject, Controls.IPlayerActions
    {
        public event Action<Vector2> OnMoveKeyPressed;
        public event Action<int> OnScrollWheelPressed;
        public event Action OnInteractKeyPressed;
        public event Action OnAttackKeyPressed;
        public event Action OnCrouchKeyPressed;
        public event Action OnSprintKeyPressed;
        public event Action OnFlashLightKeyPressed;

        public Vector2 LastMoveDir { get; private set; }
        public Vector2 CurrentMoveDir { get; private set; }
        [SerializeField] private float scrollInterval = 0.1f;
        private float _lastScrollTime;

        private Controls _control;
        private void OnEnable()
        {
            _control ??= new Controls();
            
            _control.Player.SetCallbacks(this);
            _control.Enable();

            _lastScrollTime = -999;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            // performed(입력 갱신) 뿐 아니라 canceled(키 뗌) 에서도 값을 읽어야
            // 키를 뗐을 때 CurrentMoveDir 이 0 으로 초기화된다. (release 시 ReadValue 는 0)
            Vector2 dir = context.ReadValue<Vector2>();
            CurrentMoveDir = dir;

            if (dir.sqrMagnitude > 0.0001f)
            {
                OnMoveKeyPressed?.Invoke(dir);
                LastMoveDir = dir;
            }
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnAttackKeyPressed?.Invoke();
            }
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnSprintKeyPressed?.Invoke();
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnCrouchKeyPressed?.Invoke();
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnInteractKeyPressed?.Invoke();
        }

        public void OnChangeWeapon(InputAction.CallbackContext context)
        {
            if (context.performed && Time.time - _lastScrollTime > scrollInterval)
            {
                _lastScrollTime = Time.time;
                var scrollValue = context.ReadValue<Vector2>();
                int value = scrollValue.y > 0 ? 1 : -1;
                OnScrollWheelPressed?.Invoke(value);
            }
        }

        public void OnFlashLight(InputAction.CallbackContext context)
        {
            if(context.performed)
                OnFlashLightKeyPressed?.Invoke();
        }

        #region Helper Methods
        
        public Vector3 GetMovementDirection()
        {
            if (Camera.main == null) return Vector3.zero;
            return GetMovementDirection(Camera.main.transform);
        }
        
        public Vector3 GetMovementDirection(Transform referenceTransform)
        {
            if (CurrentMoveDir.sqrMagnitude <= 0.001f || referenceTransform == null) 
                return Vector3.zero;

            Vector3 forward = referenceTransform.forward;
            Vector3 right = referenceTransform.right;

            forward.y = 0f;
            right.y = 0f;
            
            forward.Normalize();
            right.Normalize();

            Vector3 moveDir = (forward * CurrentMoveDir.y) + (right * CurrentMoveDir.x);
            return moveDir.magnitude > 1f ? moveDir.normalized : moveDir;
        }
        
        #endregion
        private void OnDisable()
        {
            if (_control != null)
            {
                _control.Disable();
            }
        }
    }
}