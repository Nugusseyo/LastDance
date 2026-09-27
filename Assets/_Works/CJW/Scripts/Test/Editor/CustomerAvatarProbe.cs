using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Test.Editor
{
    /// <summary>진단용. 손님 프리팹마다 Animator가 어떤 아바타를 쓰는지, 그 아바타의 뼈가 실제 모델에 있는지,
    /// 모듈들이 어느 Animator를 가리키는지를 Temp/CustomerSim/avatar.txt에 남긴다. 확인이 끝나면 지워도 된다.</summary>
    public static class CustomerAvatarProbe
    {
        private const string Folder = "Assets/_Works/CJW/Prefabs/Customers";

        [MenuItem("Tools/CJW/Dump Customer Avatars")]
        public static void Dump()
        {
            var sb = new StringBuilder();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                sb.AppendLine($"== {Path.GetFileNameWithoutExtension(path)}");

                foreach (Animator animator in root.GetComponentsInChildren<Animator>(true))
                {
                    Avatar avatar = animator.avatar;
                    string avatarPath = avatar != null ? AssetDatabase.GetAssetPath(avatar) : "-";
                    sb.AppendLine($"  Animator {PathOf(animator.transform, root.transform)} " +
                                  $"avatar={(avatar != null ? avatar.name : "없음")} ({avatarPath}) " +
                                  $"valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman} " +
                                  $"controller={(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "없음")} " +
                                  $"rootMotion={animator.applyRootMotion}");

                    if (avatar != null && avatar.isHuman)
                    {
                        // 아바타가 기대하는 뼈 이름이 이 Animator 아래에 실제로 있는지. 없으면 다른 모델의 아바타다.
                        var names = new HashSet<string>(animator.GetComponentsInChildren<Transform>(true).Select(t => t.name));
                        HumanBone[] bones = avatar.humanDescription.human;
                        string[] missing = bones.Where(b => !names.Contains(b.boneName)).Select(b => $"{b.humanName}:{b.boneName}").ToArray();
                        sb.AppendLine($"    뼈 {bones.Length}개 중 모델에 없는 뼈 {missing.Length}개" +
                                      (missing.Length > 0 ? " → " + string.Join(", ", missing.Take(6)) : ""));

                        // 뼈 이름이 같아도 기준 자세(T-포즈)가 다르면 비틀린다. 아바타의 skeleton 기준값과 모델 뼈의 로컬 회전을 비교한다.
                        var byName = animator.GetComponentsInChildren<Transform>(true)
                            .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                        int rotDiff = 0;
                        var worst = new List<string>();
                        foreach (SkeletonBone sk in avatar.humanDescription.skeleton)
                        {
                            if (!bones.Any(b => b.boneName == sk.name) || !byName.TryGetValue(sk.name, out Transform t))
                            {
                                continue;
                            }

                            float angle = Quaternion.Angle(sk.rotation, t.localRotation);
                            if (angle > 5f)
                            {
                                rotDiff++;
                                worst.Add($"{sk.name} {angle:F0}°");
                            }
                        }

                        sb.AppendLine($"    기준 자세와 5° 넘게 다른 뼈 {rotDiff}개" + (worst.Count > 0 ? " → " + string.Join(", ", worst.Take(6)) : ""));
                    }
                }

                foreach (SkinnedMeshRenderer smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Animator owner = smr.GetComponentInParent<Animator>(true);
                    sb.AppendLine($"  Skin {PathOf(smr.transform, root.transform)} rootBone={(smr.rootBone != null ? PathOf(smr.rootBone, root.transform) : "없음")} " +
                                  $"owner={(owner != null ? PathOf(owner.transform, root.transform) : "없음")} active={ActiveBelowRoot(smr.transform, root.transform)} " +
                                  $"mesh={(smr.sharedMesh != null ? smr.sharedMesh.name : "없음")}");
                }

                // 모듈들이 들고 있는 animator 참조. 모델을 갈아끼운 변형에서 옛 모델의 Animator를 가리키는 일이 잦다.
                foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null)
                    {
                        continue;
                    }

                    var so = new SerializedObject(mb);
                    SerializedProperty it = so.GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is Animator a)
                        {
                            sb.AppendLine($"  {mb.GetType().Name}.{it.propertyPath} → {PathOf(a.transform, root.transform)}");
                        }
                    }
                }
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.WriteAllText("Temp/CustomerSim/avatar.txt", sb.ToString());
        }

        /// <summary>플레이 중 진단. 살아 있는 손님마다 지금 재생 중인 Animator 상태, 몸(Visual)이 본체에서 얼마나 어긋났는지,
        /// 엉덩이가 바닥에서 얼마나 떠 있는지를 Temp/CustomerSim/pose.txt에 남긴다. 땅에 박히거나 떠 있는 손님을 숫자로 찾는다.</summary>
        [MenuItem("Tools/CJW/Dump Customer Poses")]
        public static void DumpPoses()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var stateNames = new Dictionary<int, string>();
            var sb = new StringBuilder();

            foreach (var customer in Object.FindObjectsByType<_Works.CJW.Scripts.Customers.AbstractCustomer>(FindObjectsSortMode.None))
            {
                Animator animator = customer.GetComponentInChildren<Animator>();
                if (animator == null)
                {
                    continue;
                }

                if (animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController ac && stateNames.Count == 0)
                {
                    foreach (var layer in ac.layers)
                    {
                        foreach (var s in layer.stateMachine.states)
                        {
                            stateNames[s.state.nameHash] = s.state.name;
                        }
                    }
                }

                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                string state = stateNames.TryGetValue(info.shortNameHash, out string n) ? n : info.shortNameHash.ToString();
                if (animator.IsInTransition(0))
                {
                    AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                    state += "→" + (stateNames.TryGetValue(next.shortNameHash, out string nn) ? nn : next.shortNameHash.ToString());
                }

                if (_walkOnly && !state.Contains("WALK"))
                {
                    continue;
                }

                Transform hips = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                Transform head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                Vector3 root = customer.transform.position;
                float ground = UnityEngine.AI.NavMesh.SamplePosition(root, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas) ? hit.position.y : float.NaN;

                sb.AppendLine($"{customer.name,-30} anim={state,-18} t={info.normalizedTime:F2} " +
                              $"visualLocal=({animator.transform.localPosition.x:F2},{animator.transform.localPosition.y:F2},{animator.transform.localPosition.z:F2}) " +
                              $"visualYaw={animator.transform.localEulerAngles.y:F0} " +
                              $"body-ground={root.y - ground:F2} visual-ground={animator.transform.position.y - ground:F2} " +
                              $"agentNext-ground={(customer.Agent != null ? customer.Agent.nextPosition.y - ground : float.NaN):F2} " +
                              $"baseOffset={(customer.Agent != null ? customer.Agent.baseOffset : float.NaN):F2} " +
                              $"updatePos={(customer.Agent != null && customer.Agent.updatePosition)} onMesh={(customer.Agent != null && customer.Agent.isOnNavMesh)} " +
                              $"hips-visual={(hips != null ? hips.position.y - animator.transform.position.y : float.NaN):F2} " +
                              $"hips-ground={(hips != null ? hips.position.y - ground : float.NaN):F2} head-ground={(head != null ? head.position.y - ground : float.NaN):F2} " +
                              $"pos=({root.x:F1},{root.z:F1}) parent={(customer.transform.parent != null ? customer.transform.parent.name : "-")} " +
                              $"animOn={animator.enabled} rootMotion={animator.applyRootMotion}");
            }

            if (_walkOnly && sb.Length == 0)
            {
                return;
            }

            Directory.CreateDirectory("Temp/CustomerSim");
            File.AppendAllText("Temp/CustomerSim/pose.txt", $"--- t={Time.time:F1}\n{sb}");
        }

        private static double _recordUntil;
        private static double _nextSample;

        /// <summary>플레이 중 진단. 걷는 동작은 짧아서 한 번 찍어서는 놓친다. 40초 동안 0.25초마다 걷는 손님만 골라 pose.txt에 남긴다.</summary>
        [MenuItem("Tools/CJW/Record Walk Poses (40s)")]
        public static void RecordWalks()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            _recordUntil = EditorApplication.timeSinceStartup + 40.0;
            _nextSample = 0;
            EditorApplication.update -= SampleWalks;
            EditorApplication.update += SampleWalks;
        }

        private static void SampleWalks()
        {
            double now = EditorApplication.timeSinceStartup;
            if (!Application.isPlaying || now > _recordUntil)
            {
                EditorApplication.update -= SampleWalks;
                File.AppendAllText("Temp/CustomerSim/pose.txt", "=== walk record done\n");
                return;
            }

            if (now < _nextSample)
            {
                return;
            }

            _nextSample = now + 0.25;
            _walkOnly = true;
            DumpPoses();
            _walkOnly = false;
        }

        private static bool _walkOnly;

        /// <summary>풀링 프리팹은 루트가 꺼진 채 저장되기도 해서 activeInHierarchy로는 알 수 없다. 루트 아래 조상만 본다.</summary>
        private static bool ActiveBelowRoot(Transform t, Transform root)
        {
            for (Transform c = t; c != null && c != root; c = c.parent)
            {
                if (!c.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        private static string PathOf(Transform t, Transform root)
        {
            if (!t.IsChildOf(root))
            {
                return "(프리팹 밖) " + t.name;
            }

            var parts = new List<string>();
            for (Transform c = t; c != root && c != null; c = c.parent)
            {
                parts.Add(c.name);
            }

            parts.Reverse();
            return parts.Count == 0 ? "(root)" : string.Join("/", parts);
        }
    }
}
