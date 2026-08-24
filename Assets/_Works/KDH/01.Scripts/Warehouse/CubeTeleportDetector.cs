using UnityEngine;

public class CubeTeleportDetector : MonoBehaviour
{
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private float detectRadius = 5f;
    [SerializeField] private LayerMask detectLayers = ~0;
    [SerializeField] private string[] detectTags = { "Player", "Car" };
    [SerializeField] private float teleportCooldown = 0.5f;

    private float lastTeleportTime = -999f;

    private void Update()
    {
        if (teleportTarget == null) return;
        if (Time.time - lastTeleportTime < teleportCooldown) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, detectRadius, detectLayers);
        foreach (var hit in hits)
        {
            if (IsDetectableTag(hit.tag))
            {
                Rigidbody rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = teleportTarget.position;
                }
                else
                {
                    hit.transform.position = teleportTarget.position;
                }

                lastTeleportTime = Time.time;
                break;
            }
        }
    }

    private bool IsDetectableTag(string colliderTag)
    {
        foreach (var t in detectTags)
        {
            if (colliderTag == t) return true;
        }
        return false;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
