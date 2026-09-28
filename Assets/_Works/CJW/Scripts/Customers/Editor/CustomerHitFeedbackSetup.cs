using System.IO;
using System.Linq;
using _Works.CJW.Scripts.Customers.Health;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>손님이 맞았을 때의 손맛 연출을 모든 손님에 붙인다. (1) 핏방울 텍스처·머티리얼·파티클 프리팹을 없으면 만들고
    /// (2) 모든 손님 프리팹에 <see cref="CustomerHitFeedbackModule"/>을 붙여 피 파티클을 꽂는다.
    /// 이미 있는 에셋과 값은 건드리지 않아 여러 번 눌러도 안전하다 — 파티클 모양을 다시 만들고 싶으면 프리팹을 지우고 누른다.</summary>
    public static class CustomerHitFeedbackSetup
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";
        private const string VfxFolder = "Assets/_Works/CJW/Data/VFX";
        private const string TexturePath = VfxFolder + "/Blood Dot.png";
        private const string MaterialPath = VfxFolder + "/Blood Particle.mat";
        private const string BloodPrefabPath = VfxFolder + "/Customer Hit Blood.prefab";

        [MenuItem("Tools/JW/Customers/Setup Hit Feedback")]
        private static void Run()
        {
            Directory.CreateDirectory(VfxFolder);

            Texture2D texture = EnsureTexture();
            Material material = EnsureMaterial(texture);
            ParticleSystem blood = EnsureBlood(material);
            if (blood == null)
            {
                Debug.LogError("[CustomerHitFeedbackSetup] 피 파티클 프리팹을 만들지 못했습니다.");
                return;
            }

            // 기반 프리팹(원본)부터 처리한다. 원본에 붙이면 변형은 물려받으니 변형에 또 붙이지 않는다.
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant)
                .ToArray();

            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<AbstractCustomer>() == null)
                    {
                        continue;
                    }

                    CustomerHitFeedbackModule module = CustomerModuleSlots.GetOrAdd<CustomerHitFeedbackModule>(root);

                    var so = new SerializedObject(module);
                    SerializedProperty bloodProperty = so.FindProperty("bloodPrefab");
                    if (bloodProperty.objectReferenceValue == null)
                    {
                        bloodProperty.objectReferenceValue = blood;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[CustomerHitFeedbackSetup] {path} 처리");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>가장자리가 부드럽게 빠지는 동그란 점. 사각형 파티클이 핏방울처럼 보이게 한다.</summary>
        private static Texture2D EnsureTexture()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture != null)
            {
                return texture;
            }

            const int size = 64;
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Clamp01((1f - d) / 0.35f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * (3f - 2f * a) * 255f));
                }
            }

            var source = new Texture2D(size, size, TextureFormat.RGBA32, false);
            source.SetPixels32(pixels);
            source.Apply();
            File.WriteAllBytes(TexturePath, source.EncodeToPNG());
            Object.DestroyImmediate(source);

            AssetDatabase.ImportAsset(TexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material EnsureMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            // 어두운 밤 장면에서 피만 빛나 보이지 않게 조명을 받는 파티클 셰이더를 쓴다.
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit") ??
                            Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material = new Material(shader) { name = "Blood Particle" };

            // 반투명(알파 블렌드). 피 안개가 가장자리부터 옅어지려면 필요하다.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static ParticleSystem EnsureBlood(Material material)
        {
            var existing = AssetDatabase.LoadAssetAtPath<ParticleSystem>(BloodPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            var root = new GameObject("Customer Hit Blood");
            try
            {
                ConfigureDrops(root.AddComponent<ParticleSystem>(), material);

                var mist = new GameObject("Mist");
                mist.transform.SetParent(root.transform, false);
                ConfigureMist(mist.AddComponent<ParticleSystem>(), material);

                PrefabUtility.SaveAsPrefabAsset(root, BloodPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<ParticleSystem>(BloodPrefabPath);
        }

        /// <summary>핏방울. 맞은 방향으로 뿜어져 포물선을 그리며 떨어지고, 바닥에 닿으면 금방 사라진다.</summary>
        private static void ConfigureDrops(ParticleSystem ps, Material material)
        {
            // 연출 모듈이 맞을 때마다 Emit으로 직접 뿜는다. 스스로는 아무것도 뿜지 않는다.
            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.02f, 0.02f), new Color(0.25f, 0f, 0f));
            main.gravityModifier = 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 300;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 30f;
            shape.radius = 0.04f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            // 바닥·벽에 닿으면 튀지 않고 멈추며 빨리 사라진다. 손님 자기 몸과 플레이어에는 부딪히지 않는다.
            ParticleSystem.CollisionModule collision = ps.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.dampen = 0.9f;
            collision.bounce = 0.05f;
            collision.lifetimeLoss = 0.6f;
            collision.collidesWith = ~LayerMaskOf("Customer", "Player", "Ignore Raycast");

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.03f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>피 안개. 맞은 자리에서 잠깐 퍼졌다 옅어진다. 방울만 있을 때보다 타격 지점이 또렷하게 보인다.</summary>
        private static void ConfigureMist(ParticleSystem ps, Material material)
        {
            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.4f, 0f, 0f, 0.55f);
            main.gravityModifier = 0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 60;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.05f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.4f));

            ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static int LayerMaskOf(params string[] names)
        {
            int mask = 0;
            foreach (string name in names)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0)
                {
                    mask |= 1 << layer;
                }
            }

            return mask;
        }
    }
}
