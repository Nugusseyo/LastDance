using UnityEngine;

namespace _Works.KDH._01.Scripts.DayNight
{
    public class SkyBodyFollower : MonoBehaviour
    {
        [SerializeField] private Transform directionSource;
        [SerializeField] private bool useOppositeDirection = true;
        [SerializeField] private float distance = 800f;
        [SerializeField] private float hideBelowHeight = -0.05f;
        [SerializeField] private Color highColor = Color.white;
        [SerializeField] private Color horizonColor = Color.white;
        [SerializeField, Range(0.01f, 1f)] private float colorChangeHeight = 0.35f;

        private Renderer bodyRenderer;
        private Material bodyMaterial;
        private Transform cameraTransform;

        private void Awake()
        {
            bodyRenderer = GetComponent<Renderer>();
            bodyMaterial = bodyRenderer.material;
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                if (Camera.main == null) return;
                cameraTransform = Camera.main.transform;
            }

            Vector3 direction = useOppositeDirection ? -directionSource.forward : directionSource.forward;

            bodyRenderer.enabled = direction.y > hideBelowHeight;

            float height = Mathf.Clamp01(direction.y / colorChangeHeight);
            bodyMaterial.color = Color.Lerp(horizonColor, highColor, height);

            transform.position = cameraTransform.position + direction * distance;
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
