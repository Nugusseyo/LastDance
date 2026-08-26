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
    [SerializeField] private float approachDistance = 1.2f;

    private Rigidbody heldPart;
    private Rigidbody selfRb;

    private void Awake()
    {
        selfRb = GetComponent<Rigidbody>();
    }

    private struct WheelSocket
    {
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    private readonly System.Collections.Generic.Dictionary<GameObject, WheelSocket> wheelSockets = new System.Collections.Generic.Dictionary<GameObject, WheelSocket>();

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (heldPart != null)
            {
                DropPart();
            }
            else if (!TryDetachPart())
            {
                TryPickupPart();
            }
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            TryAttachWheel();
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

    private bool TryDetachPart()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, partLayerMask)) return false;

        GameObject part = hit.collider.gameObject;
        Transform car = part.transform.parent;
        if (car == null) return false; // already detached, let caller try pickup instead

        Vector3 wheelDropPoint = part.transform.position;

        if (hit.collider.gameObject.layer == LayerMask.NameToLayer("Wheel") && !wheelSockets.ContainsKey(part))
        {
            wheelSockets[part] = new WheelSocket
            {
                parent = car,
                localPosition = part.transform.localPosition,
                localRotation = part.transform.localRotation
            };
        }

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

        return true;
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

        float groundY = startPos.y;
        Vector3 groundProbe = new Vector3(wheelDropPoint.x, startPos.y + 3f, wheelDropPoint.z);
        if (Physics.Raycast(groundProbe, Vector3.down, out RaycastHit groundHit, 10f, groundLayerMask))
        {
            groundY = groundHit.point.y;
        }

        Vector3 endPos = new Vector3(startPos.x, Mathf.Min(startPos.y, groundY), startPos.z);
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

    private void TryAttachWheel()
    {
        if (heldPart == null) return;
        if (!wheelSockets.TryGetValue(heldPart.gameObject, out WheelSocket socket)) return;
        if (socket.parent == null) return;

        float sqrDist = (transform.position - socket.parent.position).sqrMagnitude;
        if (sqrDist > pickupDistance * pickupDistance) return;

        GameObject wheel = heldPart.gameObject;
        wheel.transform.SetParent(socket.parent);
        wheel.transform.localPosition = socket.localPosition;
        wheel.transform.localRotation = socket.localRotation;

        heldPart.linearVelocity = Vector3.zero;
        heldPart.angularVelocity = Vector3.zero;
        heldPart.isKinematic = true;
        heldPart.useGravity = false;
        heldPart = null;
    }

    private void TryPickupPart()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance, partLayerMask)) return;

        Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
        if (rb == null || rb.transform.parent != null) return;

        Vector3 approachPos = hit.point - ray.direction.normalized * approachDistance;
        approachPos.y = transform.position.y;
        if (selfRb != null)
        {
            selfRb.linearVelocity = Vector3.zero;
            selfRb.position = approachPos;
        }
        transform.position = approachPos;

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
