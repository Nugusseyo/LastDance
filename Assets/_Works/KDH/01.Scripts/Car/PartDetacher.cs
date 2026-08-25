using UnityEngine;
using UnityEngine.InputSystem;

public class PartDetacher : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private LayerMask partLayerMask;
    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private float popForce = 5f;
    [SerializeField] private float upForce = 3f;
    [SerializeField] private float collapseTiltAngle = 15f;
    [SerializeField] private float collapseDuration = 0.6f;

    [SerializeField] private Transform holdPoint;
    [SerializeField] private float pickupDistance = 4f;

    private Rigidbody heldPart;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryDetachPart();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (heldPart != null)
            {
                DropPart();
            }
            else
            {
                TryPickupPart();
            }
        }
    }

    private void TryDetachPart()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, partLayerMask)) return;

        GameObject part = hit.collider.gameObject;
        Transform car = part.transform.parent;
        Vector3 wheelDropPoint = part.transform.position;
        part.transform.parent = null;

        Rigidbody rb = part.GetComponent<Rigidbody>();
        if (rb == null) rb = part.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;

        Vector3 outward = (part.transform.position - car.position);
        outward.y = 0f;
        outward.Normalize();

        Vector3 launchDir = outward * popForce + Vector3.up * upForce;
        rb.AddForce(launchDir, ForceMode.VelocityChange);

        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Wheel"))
        {
            CollapseCar(car, wheelDropPoint);
        }
    }

    private void CollapseCar(Transform car, Vector3 wheelDropPoint)
    {
        CarStraightMover mover = car.GetComponent<CarStraightMover>();
        if (mover != null) mover.Stop();

        StartCoroutine(CollapseRoutine(car, wheelDropPoint));
    }

    private System.Collections.IEnumerator CollapseRoutine(Transform car, Vector3 wheelDropPoint)
    {
        Vector3 localCorner = car.InverseTransformPoint(wheelDropPoint);
        localCorner.y = 0f;

        Vector3 tiltAxis = Vector3.Cross(Vector3.up, localCorner.normalized);

        Vector3 startPos = car.position;
        Quaternion startRot = car.rotation;

        float dropHeight = 0f;
        if (Physics.Raycast(wheelDropPoint, Vector3.down, out RaycastHit groundHit, 3f, groundLayerMask))
        {
            dropHeight = Mathf.Max(0f, wheelDropPoint.y - groundHit.point.y);
        }

        Vector3 endPos = startPos + Vector3.down * dropHeight;
        Quaternion endRot = startRot * Quaternion.AngleAxis(collapseTiltAngle, tiltAxis);

        float t = 0f;
        while (t < collapseDuration)
        {
            t += Time.deltaTime;
            float progress = t / collapseDuration;
            car.position = Vector3.Lerp(startPos, endPos, progress);
            car.rotation = Quaternion.Slerp(startRot, endRot, progress);
            yield return null;
        }

        car.position = endPos;
        car.rotation = endRot;
    }

    private void TryPickupPart()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance, partLayerMask)) return;

        Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
        if (rb == null || rb.transform.parent != null) return;

        heldPart = rb;
        heldPart.isKinematic = true;
        heldPart.transform.SetParent(holdPoint);
        heldPart.transform.localPosition = Vector3.zero;
        heldPart.transform.localRotation = Quaternion.identity;
    }

    private void DropPart()
    {
        heldPart.transform.SetParent(null);
        heldPart.isKinematic = false;
        heldPart = null;
    }
}
