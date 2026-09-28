using System.Linq;
using _Works.CJW.Scripts.Customers.Animation;
using _Works.CJW.Scripts.Customers.Health;
using _Works.CJW.Scripts.Customers.Ragdoll;
using DevLib.AnimatorSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>모든 손님이 플레이어에게 맞을 수 있게 한다. (1) 손님 Animator에 피격 상태(HIT)와 그 해시 에셋을 만들고
    /// (2) 모든 손님 프리팹을 Customer 레이어로 옮겨 플레이어 공격 판정에 걸리게 하고, 체력·래그돌 모듈이 없으면
    /// 기준 프리팹(Refueling Customer)의 설정을 베껴 붙인 뒤 피격 클립을 꽂는다. 여러 번 눌러도 안전하다.</summary>
    public static class CustomerHitSetup
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";
        private const string SourcePrefab = PrefabFolder + "/Refueling Customer.prefab";
        private const string ActionSourcePrefab = PrefabFolder + "/Brawler Customer.prefab";
        private const string ControllerPath = "Assets/Charactor/Animation/Customer Controller.controller";
        private const string HashPath = "Assets/_Works/CJW/Data/ParamHash/HIT param.asset";

        /// <summary>피격 클립. 전용 피격 모션이 아직 없어 비틀거리는 모션을 임시로 쓴다. 전용 클립이 들어오면 HIT 상태의 모션만 바꾸면 된다.</summary>
        private const string HitMotionPath = "Assets/Charactor/Animation/iDLE/Injured Stumble Idle.anim";

        private const string StateName = "HIT";
        private const string CustomerLayerName = "Customer";

        [MenuItem("Tools/JW/Customers/Make All Customers Hittable")]
        private static void Run()
        {
            HashDataSO hitHash = EnsureHash();
            EnsureAnimatorState();

            int layer = LayerMask.NameToLayer(CustomerLayerName);
            if (layer < 0)
            {
                Debug.LogError($"[CustomerHitSetup] '{CustomerLayerName}' 레이어가 없습니다.");
                return;
            }

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
            var sourceHealth = CustomerModuleSlots.Find<CustomerHealthModule>(source);
            var sourceRagdoll = CustomerModuleSlots.Find<RagdollModule>(source);
            if (sourceHealth == null || sourceRagdoll == null)
            {
                Debug.LogError($"[CustomerHitSetup] 기준 프리팹 {SourcePrefab}에 체력·래그돌 모듈이 없습니다.");
                return;
            }

            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant)
                .ToArray();

            GameObject actionSource = AssetDatabase.LoadAssetAtPath<GameObject>(ActionSourcePrefab);
            var sourceAction = CustomerModuleSlots.Find<ActionAnimatorModule>(actionSource);
            if (sourceAction == null)
            {
                Debug.LogError($"[CustomerHitSetup] {ActionSourcePrefab}에 연출 모듈이 없습니다.");
                return;
            }

            // 다른 손님 프리팹의 기반(변형의 원본)인 프리팹.
            var basePaths = new System.Collections.Generic.HashSet<string>(paths
                .Select(p => PrefabUtility.GetCorrespondingObjectFromSource(AssetDatabase.LoadAssetAtPath<GameObject>(p)))
                .Where(src => src != null)
                .Select(AssetDatabase.GetAssetPath));

            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (root.GetComponent<AbstractCustomer>() == null)
                    {
                        continue;
                    }

                    // 플레이어 공격은 Customer 레이어의 몸통(루트 콜라이더)만 찾는다. 래그돌 뼈 콜라이더는 쓰러졌을 때만 켜지므로 루트만 옮긴다.
                    root.layer = layer;

                    if (CustomerModuleSlots.Find<RagdollModule>(root) == null)
                    {
                        EditorUtility.CopySerialized(sourceRagdoll, CustomerModuleSlots.Add<RagdollModule>(root));
                    }

                    CustomerHealthModule health = CustomerModuleSlots.Find<CustomerHealthModule>(root);
                    if (health == null)
                    {
                        health = CustomerModuleSlots.Add<CustomerHealthModule>(root);
                        EditorUtility.CopySerialized(sourceHealth, health);
                    }

                    var so = new SerializedObject(health);
                    so.FindProperty("hitClip").objectReferenceValue = hitHash;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    // 피격 클립은 연출 모듈이 튼다. 없으면 맞아도 움찔하지 않는다.
                    // 다른 프리팹의 기반이 되는 프리팹에는 붙이지 않는다 — 이미 자기 연출 모듈을 가진 변형에 두 개가 겹친다.
                    if (CustomerModuleSlots.Find<ActionAnimatorModule>(root) == null && !basePaths.Contains(path))
                    {
                        EditorUtility.CopySerialized(sourceAction, CustomerModuleSlots.Add<ActionAnimatorModule>(root));
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[CustomerHitSetup] {path} 처리");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static HashDataSO EnsureHash()
        {
            var hash = AssetDatabase.LoadAssetAtPath<HashDataSO>(HashPath);
            if (hash == null)
            {
                hash = ScriptableObject.CreateInstance<HashDataSO>();
                AssetDatabase.CreateAsset(hash, HashPath);
            }

            var so = new SerializedObject(hash);
            so.FindProperty("<HashName>k__BackingField").stringValue = StateName;
            so.FindProperty("<HashValue>k__BackingField").intValue = Animator.StringToHash(StateName);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hash);
            return hash;
        }

        private static void EnsureAnimatorState()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(HitMotionPath);
            if (controller == null || motion == null)
            {
                Debug.LogError("[CustomerHitSetup] 손님 Animator 또는 피격 클립을 찾지 못했습니다.");
                return;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            if (machine.states.Any(s => s.state.name == StateName))
            {
                return;
            }

            // 다른 연출 상태처럼 전이 없이 둔다. 연출 모듈이 크로스페이드로 직접 틀고, 끝나면 이동 모듈이 걷기·서기로 되돌린다.
            AnimatorState state = machine.AddState(StateName, new Vector3(300f, 400f, 0f));
            state.motion = motion;
            EditorUtility.SetDirty(controller);
        }
    }
}
