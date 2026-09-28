using UnityEngine;

namespace _Works.CJW.Scripts.Title
{
    /// <summary>타이틀 카메라를 시작 위치 둘레로 천천히 흔들며 한 점을 바라보게 한다. 멈춘 화면처럼 보이지 않게 하는 정도의 움직임.</summary>
    public class TitleCameraDrift : MonoBehaviour
    {
        [Tooltip("바라볼 점. 비우면 시작할 때 바라보던 방향을 유지한다.")]
        [SerializeField] private Transform lookTarget;

        [Tooltip("시작 위치에서 좌우(x)·위아래(y)·앞뒤(z)로 흔들리는 폭(m). 카메라 로컬 축 기준.")]
        [SerializeField] private Vector3 amplitude = new(3f, 0.6f, 1.5f);

        [Tooltip("한 번 왕복하는 데 걸리는 시간(초).")]
        [SerializeField, Min(1f)] private float period = 24f;

        private Vector3 _origin;
        private Quaternion _originRotation;

        private void Awake()
        {
            _origin = transform.position;
            _originRotation = transform.rotation;
        }

        private void LateUpdate()
        {
            float t = Time.time * Mathf.PI * 2f / period;
            // 축마다 주기를 조금씩 어긋나게 해 같은 궤적을 반복하는 티가 덜 나게 한다.
            var offset = new Vector3(Mathf.Sin(t) * amplitude.x, Mathf.Sin(t * 0.73f + 1.3f) * amplitude.y, Mathf.Sin(t * 0.51f + 0.4f) * amplitude.z);
            transform.position = _origin + _originRotation * offset;

            transform.rotation = lookTarget != null
                ? Quaternion.LookRotation(lookTarget.position - transform.position, Vector3.up)
                : _originRotation;
        }
    }
}
