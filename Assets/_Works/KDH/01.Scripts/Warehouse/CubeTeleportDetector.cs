using UnityEngine;

public class CubeTeleportDetector : MonoBehaviour
{
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private float detectRadius = 5f;
    [SerializeField] private string[] tags = { "Player", "Car" };
    [SerializeField] private float teleportCooldown = 0.5f;
    [SerializeField] private float scanInterval = 0.2f;
    [SerializeField] private Vector3 playerOffset = new Vector3(2f, 0f, 0f);

    private float lastTeleportTime = -999f;
    private float lastScanTime = -999f;

    private void Update()
    {
        if (teleportTarget == null) return;
        if (Time.time - lastTeleportTime < teleportCooldown) return;
        if (Time.time - lastScanTime < scanInterval) return;
        lastScanTime = Time.time;

        foreach (var t in tags)
        {
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(t);
            foreach (var candidate in candidates)
            {
                float sqrDist = (candidate.transform.position - transform.position).sqrMagnitude;
                if (sqrDist > detectRadius * detectRadius) continue;

                Vector3 destination = teleportTarget.position;
                if (t == "Player") destination += playerOffset;

                Rigidbody rb = candidate.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = destination;
                }
                else
                {
                    candidate.transform.position = destination;
                }

                CarStraightMover mover = candidate.GetComponent<CarStraightMover>();
                if (mover != null)
                {
                    mover.Stop();
                }

                lastTeleportTime = Time.time;
                return;
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
