using UnityEngine;

public class CubeTeleportDetector : MonoBehaviour
{
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private float detectRadius = 5f;
    [SerializeField] private string[] tags = { "Player", "Car" };
    [SerializeField] private float teleportCooldown = 0.5f;
    [SerializeField] private float scanInterval = 0.2f;

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

                Rigidbody rb = candidate.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.position = teleportTarget.position;
                }
                else
                {
                    candidate.transform.position = teleportTarget.position;
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
