using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Wrench
{
    public class WheelDetacher : MonoBehaviour
    {
        [SerializeField] private EventChannelSO durationChannel;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float detachDistance = 4f;
        [SerializeField] private float aimDotThreshold = 0.9f;
        [SerializeField] private LayerMask wheelLayerMask;
        [SerializeField] private float handDetachTime = 6f;
        [SerializeField] private WrenchTool equippedWrench;
        [SerializeField] private float popForce = 5f;

        private WheelCollider[] cachedWheels = new WheelCollider[0];
        private float nextRefreshTime;
        private float holdTime;
        private GameObject targetWheel;
        private bool progressShown;

        private void Update()
        {
            RefreshWheelCache();

            GameObject hitWheelObject = FindAimedWheel();

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
                    return;
                }

                ShowProgress(holdTime, requiredTime);
            }
            else
            {
                holdTime = 0f;
                HideProgress();
            }
        }

        private void ShowProgress(float current, float max)
        {
            progressShown = true;
            if (durationChannel == null) return;

            durationChannel.RaiseEvent(UIEvents.DurationEvent.Init(current, max));
        }

        private void HideProgress()
        {
            if (!progressShown) return;

            progressShown = false;
            if (durationChannel == null) return;

            durationChannel.RaiseEvent(UIEvents.DurationEvent.Init(1f, 1f));
        }

        private void RefreshWheelCache()
        {
            if (Time.time < nextRefreshTime) return;

            nextRefreshTime = Time.time + 0.5f;
            cachedWheels = FindObjectsByType<WheelCollider>(FindObjectsSortMode.None);
        }

        private GameObject FindAimedWheel()
        {
            Vector3 cameraPosition = playerCamera.transform.position;
            GameObject bestWheel = null;
            float bestDot = aimDotThreshold;

            Collider[] nearbyColliders = Physics.OverlapSphere(cameraPosition, detachDistance, wheelLayerMask);
            for (int i = 0; i < nearbyColliders.Length; i++)
            {
                CheckWheel(nearbyColliders[i].gameObject, cameraPosition, ref bestWheel, ref bestDot);
            }

            for (int i = 0; i < cachedWheels.Length; i++)
            {
                if (cachedWheels[i] == null) continue;

                GameObject wheel = cachedWheels[i].gameObject;
                if (((1 << wheel.layer) & wheelLayerMask) == 0) continue;
                if ((wheel.transform.position - cameraPosition).sqrMagnitude > detachDistance * detachDistance) continue;

                CheckWheel(wheel, cameraPosition, ref bestWheel, ref bestDot);
            }

            return bestWheel;
        }

        private void CheckWheel(GameObject wheel, Vector3 cameraPosition, ref GameObject bestWheel, ref float bestDot)
        {
            Vector3 directionToWheel = (wheel.transform.position - cameraPosition).normalized;
            float dot = Vector3.Dot(playerCamera.transform.forward, directionToWheel);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestWheel = wheel;
            }
        }

        private void DetachWheel(GameObject wheel)
        {
            Transform car = wheel.transform.parent;
            if (car == null) return;

            wheel.transform.SetParent(null);

            WheelCollider wheelCollider = wheel.GetComponent<WheelCollider>();
            if (wheelCollider != null)
            {
                Destroy(wheelCollider);
                AddSolidCollider(wheel);
            }

            Rigidbody rb = wheel.GetComponent<Rigidbody>();
            if (rb == null) rb = wheel.AddComponent<Rigidbody>();

            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 outward = wheel.transform.position - car.position;
            outward.y = 0f;
            if (outward.sqrMagnitude > 0.001f) outward.Normalize();

            rb.AddForce(outward * popForce, ForceMode.VelocityChange);
        }

        private void AddSolidCollider(GameObject wheel)
        {
            SphereCollider sphere = wheel.AddComponent<SphereCollider>();

            Renderer wheelRenderer = wheel.GetComponentInChildren<Renderer>();
            if (wheelRenderer == null) return;

            Bounds bounds = wheelRenderer.bounds;
            Vector3 scale = wheel.transform.lossyScale;
            float maxScale = Mathf.Max(scale.x, scale.y, scale.z);
            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);

            sphere.center = wheel.transform.InverseTransformPoint(bounds.center);
            sphere.radius = maxExtent / maxScale;
        }

        private void ResetHold()
        {
            holdTime = 0f;
            targetWheel = null;
            HideProgress();
        }
    }
}
