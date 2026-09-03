using UnityEngine;

public class CubeTeleportDetector : MonoBehaviour
{
    [SerializeField] private Transform teleportTarget;
    [SerializeField] private float detectRadius = 5f;
    [SerializeField] private string[] tags = { "Player", "Car" };
    [SerializeField] private float teleportCooldown = 0.5f;
    [SerializeField] private float scanInterval = 0.2f;
    [SerializeField] private Vector3 playerOffset = new Vector3(2f, 0f, 0f);

    [System.Serializable]
    private struct VerticalNudgeOverride
    {
        [Tooltip("오브젝트 이름에 이 문자열이 포함되면 이 보정값을 쓴다. 예: Car4")]
        public string nameContains;
        public float nudge;
    }

    [SerializeField] private float defaultVerticalNudge = 0f;
    [SerializeField] private VerticalNudgeOverride[] verticalNudgeOverrides = { };

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
                destination.y += GetPivotHeightAboveBase(candidate.transform) + GetVerticalNudge(candidate.name);
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

    // Car4처럼 피벗이 바퀴 바닥보다 위에 있는 오브젝트는 목표 지점의 y를 그대로
    // 쓰면 바닥에 파묻힌다(침수차). 자신의 렌더러 바운즈로 피벗-바닥 높이차를
    // 구해서 그만큼 목적지 y를 올려준다. SUV처럼 이미 차이가 거의 없는 경우엔
    // 결과가 사실상 그대로라 기존 동작을 해치지 않는다.
    private static float GetPivotHeightAboveBase(Transform candidate)
    {
        Renderer[] renderers = candidate.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return 0f;

        float minY = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            minY = Mathf.Min(minY, renderers[i].bounds.min.y);
        }

        return candidate.position.y - minY;
    }

    // GetPivotHeightAboveBase가 자동으로 대부분 처리하지만, 예외적으로 특정
    // 오브젝트에 추가 미세 보정이 필요해지면 이름으로 구분해 여기에 등록한다.
    // 일치하는 항목이 없으면 기본값을 쓴다.
    private float GetVerticalNudge(string candidateName)
    {
        for (int i = 0; i < verticalNudgeOverrides.Length; i++)
        {
            if (!string.IsNullOrEmpty(verticalNudgeOverrides[i].nameContains) &&
                candidateName.Contains(verticalNudgeOverrides[i].nameContains))
            {
                return verticalNudgeOverrides[i].nudge;
            }
        }

        return defaultVerticalNudge;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}
