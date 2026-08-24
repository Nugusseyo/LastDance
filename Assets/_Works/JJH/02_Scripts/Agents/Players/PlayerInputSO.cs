using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.JJH._02_Scripts.Agents.Players
{
    [CreateAssetMenu(fileName = "Player Input", menuName = "SO/Player Input")]
    public class PlayerInputSO : ScriptableObject, Controls.IPlayerActions
    {
        public event Action<Vector2> OnMovementChange;
        public event Action OnAttackKeyPressed;
        public event Action OnInteractKeyPressed;
        public event Action OnChangeWeaponKeyPressed;

        public Vector2 MoveDirection { get; private set; }
        public Vector2 LookDirection { get; private set; }
        public bool IsSprinting { get; private set; }


        private Controls _control;

        private void OnEnable()
        {
            if (_control == null)
            {
                _control = new Controls();
                _control.Player.SetCallbacks(this);
            }

            _control.Player.Enable();
        }

        private void OnDisable()
        {
            if (_control != null)
                _control.Player.Disable();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            LookDirection = context.ReadValue<Vector2>();
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            MoveDirection = context.ReadValue<Vector2>();
            OnMovementChange?.Invoke(MoveDirection);
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
                OnAttackKeyPressed?.Invoke();
        }

        public void OnSprint(InputAction.CallbackContext context)
        {
            if (context.started)
                IsSprinting = true;
            else if (context.canceled)
                IsSprinting = false;
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
                OnInteractKeyPressed?.Invoke();
        }

        public void OnChangeWeapon(InputAction.CallbackContext context)
        {
            if (context.performed)
                OnChangeWeaponKeyPressed?.Invoke();
        }
    }
}