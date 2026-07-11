using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts
{
    public class DoorRotate : MonoBehaviour
    {
        [SerializeField] private float openAngle = 75f;
        [SerializeField] private float duration = 0.6f;
        [SerializeField] private Ease openEase = Ease.OutSine;
        [SerializeField] private Ease closeEase = Ease.InSine;
        [SerializeField] private Key toggleKey = Key.E;

        private Quaternion closedRotation;
        private bool isOpen;
        private Tween rotateTween;

        private void Awake()
        {
            closedRotation = transform.localRotation;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                Toggle();
            }
        }

        private void Toggle()
        {
            if (isOpen) Close();
            else Open();
        }

        private void Open()
        {
            if (isOpen) return;
            isOpen = true;

            rotateTween?.Kill();
            rotateTween = transform.DOLocalRotate(
                closedRotation.eulerAngles + new Vector3(0f, openAngle, 0f),
                duration,
                RotateMode.FastBeyond360
            ).SetEase(openEase);
        }

        private void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            rotateTween?.Kill();
            rotateTween = transform.DOLocalRotate(
                closedRotation.eulerAngles,
                duration,
                RotateMode.FastBeyond360
            ).SetEase(closeEase);
        }
    }
}
