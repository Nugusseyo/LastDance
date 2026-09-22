using System;
using _Works.JJH._02_Scripts.Agents.Players;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace _Works.JYG._Scripts.UI.Keyboard_Viewer
{
    public class KeyboardViewer : MonoBehaviour
    {
        [SerializeField] private Key targetKey = Key.E; //Key는 New InputSystem에서, 키보드의 키들을 넣어놓은 enum이다.

        [SerializeField] private UIInputSO uiInput;
        [SerializeField] private PlayerInputSO playerInput;

        public UnityEvent OnKeyPressed;

        private void Update()
        {
            if (Keyboard.current[targetKey].wasPressedThisFrame)
            {
                uiInput.SetEnable(true);
                playerInput.SetEnable(false);
                OnKeyPressed?.Invoke();

                uiInput.OnEscapePressed += HandleEscapeKeyPressed;
            }
        }

        private void HandleEscapeKeyPressed()
        {
            uiInput.SetEnable(false);
            playerInput.SetEnable(true);
            
            uiInput.OnEscapePressed -= HandleEscapeKeyPressed;
        }
    }
}
