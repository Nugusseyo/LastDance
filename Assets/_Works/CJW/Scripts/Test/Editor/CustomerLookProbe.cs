using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>임시 진단. 손님 겉모습(CustomerAppearanceModule)이 제대로 입혀졌는지 확인한다. 확인이 끝나면 지워도 된다.</summary>
    public static class CustomerLookProbe
    {
        /// <summary>에디터 진단. 손님 프리팹 뼈대와 겉모습 후보의 뼈를 CustomerAppearanceModule과 같은 규칙(경로, 사본은 마지막 Root부터)으로 맞춰 보고, 못 찾는 뼈를 Temp/CustomerSim/bones.txt에 남긴다.</summary>
        [MenuItem("Tools/CJW/Check Customer Look Bones")]
        private static void CheckBones()
        {
            var sb = new StringBuilder();
            var set = AssetDatabase.LoadAssetAtPath<_Works.CJW.Scripts.Customers.Appearance.CustomerLookSetSO>("Assets/_Works/CJW/Data/Customers/Customer Look Set.asset");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Works/CJW/Prefabs/Customers" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                Animator anim = prefab.GetComponentInChildren<Animator>(true);
                if (anim == null) continue;
                Transform root = anim.transform.Find("Root");
                sb.AppendLine($"== {prefab.name} visual={anim.name} root={(root != null)}");
                if (root == null) continue;

                if (set == null) continue;
                var byPath = new System.Collections.Generic.HashSet<string>();
                foreach (Transform b in root.GetComponentsInChildren<Transform>(true)) byPath.Add(Path(b, anim.transform));
                foreach (GameObject look in set.Looks)
                {
                    foreach (SkinnedMeshRenderer smr in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        foreach (Transform b in smr.bones)
                        {
                            if (b == null) continue;
                            string p = Path(b, look.transform);
                            int nested = p.LastIndexOf("/Root/", System.StringComparison.Ordinal);
                            if (nested >= 0) p = p.Substring(nested + 1);
                            if (!byPath.Contains(p)) sb.AppendLine($"  MISS {look.name}/{smr.name}: {p}");
                        }
                    }
                }
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/bones.txt", sb.ToString());
            Debug.Log("[CustomerLookProbe] Temp/CustomerSim/bones.txt");
        }

        private static string Path(Transform t, Transform root)
        {
            string p = t.name;
            while (t.parent != null && t.parent != root) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        /// <summary>플레이 중 진단. 살아 있는 손님마다 입은 겉모습과, 뼈가 비어 있는 렌더러 수를 Temp/CustomerSim/looks.txt에 남긴다.</summary>
        [MenuItem("Tools/CJW/Dump Customer Looks")]
        private static void DumpLooks()
        {
            var sb = new StringBuilder();
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var customer in Object.FindObjectsByType<_Works.CJW.Scripts.Customers.AbstractCustomer>(FindObjectsSortMode.None))
            {
                GameObject look = customer.Appearance?.CurrentLook;
                int active = 0, broken = 0;
                foreach (SkinnedMeshRenderer smr in customer.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    active++;
                    foreach (Transform b in smr.bones)
                    {
                        // 비었거나 머리카락 속 뼈대 사본(움직이지 않음)에 붙은 뼈
                        if (b == null || Path(b, customer.transform).Contains("/npc_hair")) { broken++; break; }
                    }
                    if (smr.sharedMesh == null || smr.rootBone == null) broken++;
                }

                // 팔 메시를 지금 포즈로 구워 양손 뼈에서 가장 가까운 정점까지 거리를 잰다. 팔이 뼈를 안 따라가면 커진다.
                string hands = "";
                Animator anim = customer.GetComponentInChildren<Animator>();
                foreach (SkinnedMeshRenderer smr in customer.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    if (anim == null || !smr.name.Contains("arms") || !smr.name.EndsWith("lod0")) continue;
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    Vector3[] verts = baked.vertices;
                    foreach (HumanBodyBones hb in new[] { HumanBodyBones.LeftHand, HumanBodyBones.RightHand })
                    {
                        Vector3 hand = anim.GetBoneTransform(hb).position;
                        float best = float.MaxValue;
                        // useScale로 구우면 크기가 이미 들어가 있어 위치·회전만 입힌다.
                        foreach (Vector3 v in verts) best = Mathf.Min(best, (smr.transform.position + smr.transform.rotation * v - hand).sqrMagnitude);
                        hands += $" {hb}={Mathf.Sqrt(best):F3}";
                    }

                    Object.DestroyImmediate(baked);
                }

                var lod = customer.GetComponentInChildren<LODGroup>();
                string name = look != null ? look.name : "없음";
                counts[name] = counts.TryGetValue(name, out int c) ? c + 1 : 1;
                sb.AppendLine($"{customer.name} active={customer.gameObject.activeInHierarchy} pos={customer.transform.position} look={name} renderers={active} broken={broken} lods={(lod != null ? lod.lodCount : 0)}{hands}");
            }

            sb.AppendLine($"-- 분포 (scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} time={Time.time:F1})");
            foreach (var kv in counts) sb.AppendLine($"{kv.Key}: {kv.Value}");
            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/looks.txt", sb.ToString());
            Debug.Log("[CustomerLookProbe] Temp/CustomerSim/looks.txt");
        }
    }
}
