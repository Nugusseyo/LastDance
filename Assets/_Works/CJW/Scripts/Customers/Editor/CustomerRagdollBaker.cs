using System.Linq;
using _Works.CJW.Scripts.Customers.Ragdoll;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Editor
{
    /// <summary>손님 프리팹의 휴머노이드 뼈에 래그돌 강체·콜라이더·관절을 미리 굽는다. <see cref="RagdollModule"/>은 런타임에 이걸 켜고 끄기만 한다.
    /// 관절은 프리팹의 기본 자세를 기준으로 만든다 — 쓰러질 때마다 그때 자세로 새로 만들던 예전 방식은 관절이 뼈를 잡아당겨 메시가 찢어졌다.
    /// 기반 프리팹부터 굽고, 이미 구운 몸(물려받은 것 포함)은 건너뛰어 여러 번 눌러도 안전하다.</summary>
    public static class CustomerRagdollBaker
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Customers";

        [MenuItem("Tools/JW/Customers/Bake Customer Ragdoll")]
        private static void Run()
        {
            string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(p)) == PrefabAssetType.Variant)
                .ToArray();

            foreach (string path in paths)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    RagdollModule module = CustomerModuleSlots.Find<RagdollModule>(root);
                    Animator animator = root.GetComponentInChildren<Animator>(true);
                    if (module == null || animator == null)
                    {
                        continue;
                    }

                    if (!animator.isHuman)
                    {
                        Debug.LogError($"[CustomerRagdollBaker] {path}의 Animator가 휴머노이드가 아닙니다.");
                        continue;
                    }

                    Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    if (hips != null && hips.GetComponent<Rigidbody>() != null)
                    {
                        continue;
                    }

                    if (!Bake(animator, module.TotalMass, module.LimbThickness))
                    {
                        Debug.LogError($"[CustomerRagdollBaker] {path}의 휴머노이드 뼈(엉덩이·척추·머리)를 찾지 못했습니다.");
                        continue;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[CustomerRagdollBaker] {path} 래그돌 굽기");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static bool Bake(Animator animator, float totalMass, float limbThickness)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || spine == null || head == null)
            {
                return false;
            }

            float height = Vector3.Distance(hips.position, head.position) * 2.2f;

            Rigidbody hipsBody = AddBody(hips, totalMass * 0.2f);
            AddBox(hips, spine, height * 0.09f);

            Rigidbody spineBody = AddBody(spine, totalMass * 0.2f);
            AddCapsule(spine, head.position, height * 0.09f);
            AddJoint(spineBody, hipsBody, 25f, 25f, 20f);

            Rigidbody headBody = AddBody(head, totalMass * 0.08f);
            AddSphere(head, height * 0.06f);
            AddJoint(headBody, spineBody, 40f, 40f, 30f);

            // 기본 자세(팔 벌린 자세)에서 걷던 자세로 팔이 크게 내려와 있어도 쓰러지는 순간 한계에 걸려 튕기지 않게 넉넉히 준다.
            Limb(animator, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, spineBody, totalMass, 0.035f, 0.025f, false, limbThickness);
            Limb(animator, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, spineBody, totalMass, 0.035f, 0.025f, false, limbThickness);
            Limb(animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, hipsBody, totalMass, 0.1f, 0.06f, true, limbThickness);
            Limb(animator, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, hipsBody, totalMass, 0.1f, 0.06f, true, limbThickness);
            return true;
        }

        private static void Limb(Animator animator, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones end, Rigidbody parent,
            float totalMass, float upperMass, float lowerMass, bool leg, float limbThickness)
        {
            Transform u = animator.GetBoneTransform(upper), l = animator.GetBoneTransform(lower), e = animator.GetBoneTransform(end);
            if (u == null || l == null)
            {
                return;
            }

            Rigidbody upperBody = AddBody(u, totalMass * upperMass);
            AddCapsule(u, l.position, Vector3.Distance(u.position, l.position) * limbThickness * 0.5f);
            AddJoint(upperBody, parent, leg ? 30f : 60f, leg ? 60f : 90f, leg ? 30f : 60f);

            Rigidbody lowerBody = AddBody(l, totalMass * lowerMass);
            Vector3 tip = e != null ? e.position : l.position + (l.position - u.position) * 0.9f;
            AddCapsule(l, tip, Vector3.Distance(l.position, tip) * limbThickness * 0.45f);

            // 굽는 축(twist)만 넉넉히, 옆으로 꺾이는 축은 좁게 둔다.
            AddJoint(lowerBody, upperBody, 100f, 10f, 5f);
        }

        /// <summary>평소 상태로 굽는다 — kinematic, 콜라이더 꺼짐. 쓰러질 때 <see cref="RagdollModule"/>이 뒤집는다.</summary>
        private static Rigidbody AddBody(Transform bone, float mass)
        {
            Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            return body;
        }

        private static void AddJoint(Rigidbody body, Rigidbody parent, float twist, float swing1, float swing2)
        {
            CharacterJoint joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.enablePreprocessing = false;
            joint.enableProjection = true;
            joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
            joint.highTwistLimit = new SoftJointLimit { limit = twist };
            joint.swing1Limit = new SoftJointLimit { limit = swing1 };
            joint.swing2Limit = new SoftJointLimit { limit = swing2 };
        }

        /// <summary>뼈에서 끝점까지 이어지는 캡슐. 뼈의 로컬 축 중 그 방향과 가장 가까운 축으로 세운다.</summary>
        private static void AddCapsule(Transform bone, Vector3 end, float radius)
        {
            Vector3 local = bone.InverseTransformPoint(end);
            CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = LargestAxis(local);

            float scale = AxisScale(bone, capsule.direction);
            capsule.center = local * 0.5f;
            capsule.radius = Mathf.Max(radius / scale, 0.01f);
            capsule.height = local.magnitude + capsule.radius;
            capsule.enabled = false;
        }

        private static void AddBox(Transform bone, Transform next, float halfWidth)
        {
            Vector3 local = bone.InverseTransformPoint(next.position);
            BoxCollider box = bone.gameObject.AddComponent<BoxCollider>();
            int axis = LargestAxis(local);
            float w = halfWidth * 2f / AxisScale(bone, axis == 0 ? 1 : 0);
            Vector3 size = new(w, w, w);
            size[axis] = Mathf.Abs(local[axis]);
            box.size = size;
            box.center = local * 0.5f;
            box.enabled = false;
        }

        private static void AddSphere(Transform bone, float radius)
        {
            SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
            sphere.radius = radius / AxisScale(bone, 1);
            sphere.center = new Vector3(0f, sphere.radius, 0f);
            sphere.enabled = false;
        }

        private static int LargestAxis(Vector3 v)
        {
            Vector3 a = new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            return a.x >= a.y && a.x >= a.z ? 0 : a.y >= a.z ? 1 : 2;
        }

        /// <summary>뼈 로컬 축 하나가 월드에서 몇 m인지. 모델이 스케일돼 있어도 콜라이더 크기를 월드 기준으로 맞춘다.</summary>
        private static float AxisScale(Transform bone, int axis)
        {
            Vector3 unit = Vector3.zero;
            unit[axis] = 1f;
            float s = bone.TransformVector(unit).magnitude;
            return s > 1e-5f ? s : 1f;
        }
    }
}
