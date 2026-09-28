using System.Collections.Generic;
using System.Linq;
using _Works.CJW.Scripts.Customers.Patience;
using _Works.CJW.Scripts.Customers.UI;
using _Works.JYG._Scripts.UI.GuestUI;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>모든 손님 머리 위에 체력(HealthGauge)·인내심(PatienceGauge) 게이지를 단다.
    /// 인내심 모듈과 게이지 표시 모듈(<see cref="CustomerGaugeModule"/>)을 붙이고 두 게이지를 꽂는다.
    /// 변형 프리팹은 원본에서 물려받으므로 원본부터 처리하고, 이미 가진 프리팹은 건너뛴다. 여러 번 눌러도 안전하다.</summary>
    public static class CustomerGaugeSetup
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";
        private const string HealthGaugePath = "Assets/_Works/JYG/GameModule/Guest/HealthGauge.prefab";
        private const string PatienceGaugePath = "Assets/_Works/JYG/GameModule/Guest/PatienceGauge.prefab";

        /// <summary>머리 꼭대기에서 체력바까지의 높이(m).</summary>
        private const float HealthAboveHead = 0.25f;

        /// <summary>머리 꼭대기에서 인내심 원까지의 높이(m). 체력바 바로 위에 둔다.</summary>
        private const float PatienceAboveHead = 0.6f;

        /// <summary>인내심 원의 크기. 원본은 1m 원이라 머리 위에 두기엔 커서 줄인다.</summary>
        private const float PatienceScale = 0.4f;

        /// <summary>렌더러를 찾지 못했을 때 쓰는 머리 꼭대기 높이(손님 원점 기준, m).</summary>
        private const float FallbackHeadTop = 0.85f;

        [MenuItem("Tools/JW/Customers/Add Health And Patience Gauges")]
        private static void Run()
        {
            GameObject healthPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthGaugePath);
            GameObject patiencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PatienceGaugePath);
            if (healthPrefab == null || patiencePrefab == null)
            {
                Debug.LogError($"[CustomerGaugeSetup] 게이지 프리팹을 찾지 못했습니다. {HealthGaugePath}, {PatienceGaugePath}");
                return;
            }

            foreach (string path in CustomerPrefabsBaseFirst())
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<AbstractCustomer>() == null)
                    {
                        continue;
                    }

                    float headTop = HeadTop(root);

                    CustomerModuleSlots.GetOrAdd<CustomerPatienceModule>(root);

                    WorldGauge health = EnsureGauge(root, healthPrefab, new Vector3(0f, headTop + HealthAboveHead, 0f), 1f);
                    WorldGauge patience = EnsureGauge(root, patiencePrefab, new Vector3(0f, headTop + PatienceAboveHead, 0f), PatienceScale);

                    CustomerGaugeModule view = CustomerModuleSlots.GetOrAdd<CustomerGaugeModule>(root);

                    var so = new SerializedObject(view);
                    so.FindProperty("healthGauge").objectReferenceValue = health;
                    so.FindProperty("patienceGauge").objectReferenceValue = patience;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[CustomerGaugeSetup] {path} 처리 (머리 높이 {headTop:F2}m)");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>손님 프리팹을 원본이 변형보다 먼저 오도록 늘어놓는다. 원본에 붙인 게이지를 변형이 물려받아, 변형에 두 벌이 겹치지 않는다.</summary>
        private static IEnumerable<string> CustomerPrefabsBaseFirst()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(VariantDepth);
        }

        private static int VariantDepth(string path)
        {
            int depth = 0;
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            while (go != null && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Variant)
            {
                go = PrefabUtility.GetCorrespondingObjectFromSource(go);
                depth++;
            }

            return depth;
        }

        /// <summary>이름이 같은 게이지가 이미 있으면(원본에서 물려받은 것 포함) 그대로 쓰고, 없으면 게이지 프리팹을 자식으로 넣는다.</summary>
        private static WorldGauge EnsureGauge(GameObject root, GameObject gaugePrefab, Vector3 localPosition, float scale)
        {
            Transform existing = root.transform.Find(gaugePrefab.name);
            if (existing != null)
            {
                return existing.GetComponent<WorldGauge>();
            }

            var gauge = (GameObject)PrefabUtility.InstantiatePrefab(gaugePrefab, root.transform);
            gauge.name = gaugePrefab.name;

            var rect = (RectTransform)gauge.transform;
            rect.localPosition = localPosition;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * scale;

            return gauge.GetComponent<WorldGauge>();
        }

        /// <summary>손님 원점에서 머리 꼭대기까지의 높이. 겉모습 메시의 경계로 잰다.</summary>
        private static float HeadTop(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is SkinnedMeshRenderer || r is MeshRenderer)
                .ToArray();

            if (renderers.Length == 0)
            {
                return FallbackHeadTop;
            }

            float top = renderers.Max(r => r.bounds.max.y);
            float height = top - root.transform.position.y;

            // 경계가 비어 있거나 터무니없으면 기본값을 쓴다. 사람 키를 넘는 값은 소품이 섞인 것이다.
            return height > 0.3f && height < 2.5f ? height : FallbackHeadTop;
        }
    }
}
