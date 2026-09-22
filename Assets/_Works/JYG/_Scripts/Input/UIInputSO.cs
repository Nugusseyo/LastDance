using System;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "UIInputSO", menuName = "Scriptable Objects/UIInputSO")]
public class UIInputSO : ScriptableObject, Controls.IUIActions
{
    public event Action OnEscapePressed;

    private Controls _controls;
    private bool _createdInput = false;

    private void OnEnable()
    {
        if(_controls == null)
        {
            _controls = new Controls();
            _createdInput = true;
        }
        
        SetUpInput();
        _controls.UI.Disable();
    }

    private void OnDisable()
    {
        if (_controls != null)
        {
            CleanUpInput();
        }
    }

    private void CleanUpInput()
    {
        if (_controls == null) return;
        _controls.UI.RemoveCallbacks(this);
        _controls.UI.Disable();

        if (_createdInput)
        {
            _controls.Dispose();    //만들어진 Controls는 Dispose로 제거해줘야 함.
            _createdInput = false;
        }
        
        _controls = null;
    }

    public void InitializeInput(Controls controls)
    {
        if (_controls != null)
            CleanUpInput();
        
        _controls = controls;   // ??구문으로 아래 if 없앨 수 있지만, 가독성이 안좋다고 판단함.

        if (_controls == null)
        {
            _controls = new Controls();
            _createdInput = true;
        }

        SetUpInput();
    }

    private void SetUpInput()
    {
        _controls.UI.SetCallbacks(this);
    }

    public void SetEnable(bool enable)
    {
        if(enable)
            _controls.UI.Enable();
        else
            _controls.UI.Disable();
    }

    public void OnEscape(InputAction.CallbackContext context)
    {
        if (context.performed)
            OnEscapePressed?.Invoke();
    }
}
