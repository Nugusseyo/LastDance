using UnityEngine;

namespace _Works.KDH._01.Scripts.Wrench
{
    public static class WheelPopper
    {
        public static void Pop(GameObject wheel, float popForce)
        {
            Transform car = wheel.transform.parent;
            if (car == null) return;

            wheel.transform.SetParent(null);

            WheelCollider wheelCollider = wheel.GetComponent<WheelCollider>();
            if (wheelCollider != null)
            {
                Object.Destroy(wheelCollider);
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

        private static void AddSolidCollider(GameObject wheel)
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
    }
}
