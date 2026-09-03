using System.Collections;
using System.Collections.Generic;
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

    // 지금 들고 있는 부품을 다른 스크립트에서도 알 수 있게 해줌 (없으면 null)
    public GameObject HeldPart => heldPart != null ? heldPart.gameObject : null;

    private struct PartSocket
    {
        public Transform parent;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    private static readonly string[] ReattachableLayers = { "Wheel", "Engine" };

    private readonly Dictionary<GameObject, PartSocket> partSockets = new System.Collections.Generic.Dictionary<GameObject, PartSocket>();

    private static bool IsReattachableLayer(int layer)
    {
        for (int i = 0; i < ReattachableLayers.Length; i++)
        {
            if (layer == LayerMask.NameToLayer(ReattachableLayers[i])) return true;
        }
        return false;
    }

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
            TryAttachPart();
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
        bool isWheel = hit.collider.gameObject.layer == LayerMask.NameToLayer("Wheel");

        if (IsReattachableLayer(hit.collider.gameObject.layer) && !partSockets.ContainsKey(part))
        {
            partSockets[part] = new PartSocket
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

        if (isWheel)
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

    private IEnumerator CollapseRoutine(Transform car, Vector3 wheelDropPoint)
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

        // 차체를 Transform으로 직접 옮기면 콜라이더가 플레이어를 밀어내지 못해
        // 내려앉는 동안 플레이어가 차체 안쪽으로 겹쳐 들어갈 수 있다.
        // Kinematic Rigidbody의 Move로 옮기면 물리 엔진이 겹침을 풀어 밀어낸다.
        Rigidbody carRb = car.GetComponent<Rigidbody>();
        bool addedRb = carRb == null;
        if (addedRb)
        {
            carRb = car.gameObject.AddComponent<Rigidbody>();
            carRb.isKinematic = true;
        }
        bool wasKinematic = carRb.isKinematic;
        carRb.isKinematic = true;

        float t = 0f;
        while (t < collapseDuration)
        {
            t += Time.deltaTime;
            float progress = t / collapseDuration;
            carRb.MovePosition(Vector3.Lerp(startPos, endPos, progress));
            carRb.MoveRotation(Quaternion.Slerp(startRot, endRot, progress));
            yield return new WaitForFixedUpdate();
        }

        carRb.MovePosition(endPos);
        carRb.MoveRotation(endRot);

        if (addedRb)
        {
            Destroy(carRb);
        }
        else
        {
            carRb.isKinematic = wasKinematic;
        }
    }

    private void TryAttachPart()
    {
        if (heldPart == null) return;
        if (!partSockets.TryGetValue(heldPart.gameObject, out PartSocket socket)) return;
        if (socket.parent == null) return;

        // 차체 피벗이 아니라 실제로 부품이 다시 꽂힐 소켓 위치를 기준으로 거리를 재야 한다.
        // 피벗 기준으로 재면 popForce로 밀려난 바퀴 소켓 위치가 이미 pickupDistance 밖일 수 있어
        // 플레이어가 아무리 가까이 가도 R을 눌러도 반응이 없는 것처럼 보인다.
        Vector3 socketWorldPos = socket.parent.TransformPoint(socket.localPosition);
        float sqrDist = (transform.position - socketWorldPos).sqrMagnitude;
        if (sqrDist > pickupDistance * pickupDistance)
        {
            Debug.Log($"[PartDetacher] 재장착 실패: 소켓까지 거리 {Mathf.Sqrt(sqrDist):F2}m (허용 {pickupDistance}m). 더 가까이 가서 R을 눌러주세요.");
            return;
        }

        GameObject part = heldPart.gameObject;
        SetPartCollidersEnabled(part, true);
        part.transform.SetParent(socket.parent);
        part.transform.localPosition = socket.localPosition;
        part.transform.localRotation = socket.localRotation;

        heldPart.linearVelocity = Vector3.zero;
        heldPart.angularVelocity = Vector3.zero;
        heldPart.isKinematic = true;
        heldPart.useGravity = false;
        partSockets.Remove(part);
        heldPart = null;
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

        // 콜라이더를 켜둔 채로 들면 플레이어 자신의 콜라이더와 겹쳐서
        // 물리엔진이 플레이어를 계속 밀어내(플레이어가 저절로 움직이는 원인).
        SetPartCollidersEnabled(heldPart.gameObject, false);
    }

    private void DropPart()
    {
        SetPartCollidersEnabled(heldPart.gameObject, true);
        heldPart.transform.SetParent(null);
        heldPart.isKinematic = false;
        heldPart = null;
    }

    private static void SetPartCollidersEnabled(GameObject part, bool enabled)
    {
        Collider[] colliders = part.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = enabled;
        }
    }
}
