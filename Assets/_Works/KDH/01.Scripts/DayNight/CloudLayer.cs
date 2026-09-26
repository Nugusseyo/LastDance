using UnityEngine;

namespace _Works.KDH._01.Scripts.DayNight
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class CloudLayer : MonoBehaviour
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private float size = 1800f;
        [SerializeField] private int gridCount = 24;
        [SerializeField] private float textureSize = 400f;
        [SerializeField, Range(0f, 1f)] private float edgeFadeStart = 0.55f;
        [SerializeField] private Vector2 windDirection = new Vector2(1f, 0.3f);
        [SerializeField, Range(0f, 20f)] private float windSpeed = 4f;
        [SerializeField] private Color dayColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color nightColor = new Color(0.15f, 0.17f, 0.25f, 0.6f);

        private Material cloudMaterial;
        private Vector2 offset;
        private float maxSunIntensity;

        private void Awake()
        {
            GetComponent<MeshFilter>().mesh = CreateMesh();

            cloudMaterial = GetComponent<MeshRenderer>().material;
            maxSunIntensity = sunLight.intensity;
            UpdateColor();
        }

        private void Update()
        {
            offset += windDirection.normalized * (windSpeed / textureSize) * Time.deltaTime;
            cloudMaterial.SetTextureOffset("_BaseMap", offset);

            UpdateColor();
        }

        private void UpdateColor()
        {
            float daylight = maxSunIntensity > 0f ? Mathf.Clamp01(sunLight.intensity / maxSunIntensity) : 0f;
            cloudMaterial.SetColor("_BaseColor", Color.Lerp(nightColor, dayColor, daylight));
        }

        private Mesh CreateMesh()
        {
            int lineCount = gridCount + 1;
            Vector3[] vertices = new Vector3[lineCount * lineCount];
            Vector2[] uvs = new Vector2[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[gridCount * gridCount * 6];
            float half = size * 0.5f;

            for (int z = 0; z < lineCount; z++)
            {
                for (int x = 0; x < lineCount; x++)
                {
                    int index = z * lineCount + x;
                    float posX = -half + size * x / gridCount;
                    float posZ = -half + size * z / gridCount;

                    vertices[index] = new Vector3(posX, 0f, posZ);
                    uvs[index] = new Vector2(posX / textureSize, posZ / textureSize);

                    float distance = new Vector2(posX, posZ).magnitude / half;
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edgeFadeStart, 1f, distance));
                    colors[index] = new Color(1f, 1f, 1f, alpha);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < gridCount; z++)
            {
                for (int x = 0; x < gridCount; x++)
                {
                    int start = z * lineCount + x;

                    triangles[triangleIndex++] = start;
                    triangles[triangleIndex++] = start + lineCount;
                    triangles[triangleIndex++] = start + 1;
                    triangles[triangleIndex++] = start + 1;
                    triangles[triangleIndex++] = start + lineCount;
                    triangles[triangleIndex++] = start + lineCount + 1;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Cloud Layer";
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
