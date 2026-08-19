using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts
{
    /// <summary>
    /// [TEST ONLY] 장롱 레이캐스트 상호작용을 테스트하기 위한 임시 마우스룩.
    /// 실제 플레이어 회전 시스템이 구현되면 이 컴포넌트는 제거한다.
    /// Yaw는 이 트랜스폼(Player)을, Pitch는 지정한 카메라 트랜스폼을 회전시킨다.
    /// </summary>
    public class TestMouseLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraPitchTransform;
        [SerializeField] private float mouseSensitivity = 5f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        private float pitch;

        private void Awake()
        {
            if (cameraPitchTransform == null) cameraPitchTransform = transform;
            pitch = cameraPitchTransform.localEulerAngles.x;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            if (Mouse.current == null) return;

            Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity * Time.deltaTime;

            transform.Rotate(Vector3.up, delta.x, Space.World);

            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
            Vector3 camEuler = cameraPitchTransform.localEulerAngles;
            cameraPitchTransform.localEulerAngles = new Vector3(pitch, camEuler.y, camEuler.z);
        }
    }
}
