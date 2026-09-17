using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Wrench
{
    public class WheelDetacher : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float detachDistance = 4f;
        [SerializeField] private float aimDotThreshold = 0.9f;
        [SerializeField] private LayerMask wheelLayerMask;
        [SerializeField] private float handDetachTime = 6f;
        [SerializeField] private WrenchTool equippedWrench;
        [SerializeField] private float popForce = 5f;

        private float holdTime;
        private GameObject targetWheel;

        private void Update()
        {
            Collider[] nearbyWheels = Physics.OverlapSphere(playerCamera.transform.position, detachDistance, wheelLayerMask);

            GameObject hitWheelObject = null;
            float bestDot = aimDotThreshold;

            for (int i = 0; i < nearbyWheels.Length; i++)
            {
                Vector3 directionToWheel = (nearbyWheels[i].transform.position - playerCamera.transform.position).normalized;
                float dot = Vector3.Dot(playerCamera.transform.forward, directionToWheel);

                if (dot > bestDot)
                {
                    bestDot = dot;
                    hitWheelObject = nearbyWheels[i].gameObject;
                }
            }

            if (hitWheelObject == null)
            {
                ResetHold();
                return;
            }

            if (targetWheel != hitWheelObject)
            {
                targetWheel = hitWheelObject;
                holdTime = 0f;
            }

            if (Keyboard.current != null && Keyboard.current.fKey.isPressed)
            {
                holdTime += Time.deltaTime;

                float requiredTime = equippedWrench != null ? equippedWrench.GetDetachTime() : handDetachTime;

                if (holdTime >= requiredTime)
                {
                    DetachWheel(targetWheel);
                    ResetHold();
                }
            }
            else
            {
                holdTime = 0f;
            }
        }

        private void DetachWheel(GameObject wheel)
        {
            Transform car = wheel.transform.parent;
            if (car == null) return;

            wheel.transform.SetParent(null);

            Rigidbody rb = wheel.GetComponent<Rigidbody>();
            if (rb == null) rb = wheel.AddComponent<Rigidbody>();

            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 outward = wheel.transform.position - car.position;
            outward.y = 0f;
            if (outward.sqrMagnitude > 0.001f) outward.Normalize();

            rb.AddForce(outward * popForce, ForceMode.VelocityChange);
        }

        private void ResetHold()
        {
            holdTime = 0f;
            targetWheel = null;
        }
    }
}
