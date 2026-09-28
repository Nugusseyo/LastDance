using System.Text;
using UnityEditor;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars.Editor
{
    /// <summary>플레이어가 차를 뚫고 지나가지 못하게 차 프리팹마다 차체 모양의 볼록 메시 콜라이더를 단다.
    /// 콜라이더는 전용 레이어(<see cref="LayerName"/>)에 두고 그 레이어는 플레이어하고만 부딪히게 한다 —
    /// 손님(탑승·래그돌), 바퀴, 아이템, 바닥과 부딪히면 탑승과 주행이 흔들린다.</summary>
    public static class CarPlayerBlockerSetup
    {
        private const string PrefabFolder = "Assets/_Works/CJW/Prefabs/Cars";
        private const string BlockerName = "PlayerBlocker";
        private const string LayerName = "CarBody";
        private const string PlayerLayerName = "Player";

        [MenuItem("Tools/JW/Cars/Add Player Blocker Colliders")]
        public static void Setup()
        {
            var sb = new StringBuilder("[CarPlayerBlocker]\n");

            int layer = EnsureLayer(sb);
            int player = LayerMask.NameToLayer(PlayerLayerName);
            if (layer < 0 || player < 0)
            {
                Debug.LogError($"[CarPlayerBlocker] 레이어를 준비하지 못했습니다. ({LayerName} {layer}, {PlayerLayerName} {player})");
                return;
            }

            SetCollisionOnlyWith(layer, player);
            sb.AppendLine($"  레이어 {LayerName}({layer})는 {PlayerLayerName}({player})하고만 부딪힘");

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (AddBlocker(root, layer, sb))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        private static bool AddBlocker(GameObject root, int layer, StringBuilder sb)
        {
            if (root.GetComponent<TestCar>() == null || root.transform.childCount == 0)
            {
                sb.AppendLine($"  {root.name}: 차가 아님, 건너뜀");
                return false;
            }

            Transform blocker = root.transform.Find(BlockerName);
            if (blocker == null)
            {
                blocker = new GameObject(BlockerName).transform;
                blocker.SetParent(root.transform, false);
            }

            blocker.gameObject.layer = layer;
            blocker.localPosition = Vector3.zero;
            blocker.localRotation = Quaternion.identity;
            blocker.localScale = Vector3.one;

            // 예전에 단 박스는 차체 메시 경계라 사이드미러 폭까지 잡혀 차 옆에 투명 벽이 생겼다. 차체 모양 그대로의 볼록 메시로 바꾼다.
            var oldBox = blocker.GetComponent<BoxCollider>();
            if (oldBox != null)
            {
                Object.DestroyImmediate(oldBox, true);
            }

            for (int i = blocker.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(blocker.GetChild(i).gameObject, true);
            }

            // Build가 모델을 첫 번째 자식으로 넣는다. 바퀴는 따로 콜라이더가 있으니 차체 메시만 쓴다.
            Transform visual = root.transform.GetChild(0);
            int count = 0;
            foreach (MeshFilter mf in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name.Contains("Wheel"))
                {
                    continue;
                }

                // 모델 안에 콜라이더를 달면 바퀴 분리 등 다른 작업이 모델 계층을 건드릴 때 같이 휩쓸린다. 차체 메시 자리를 본뜬 자식에 단다.
                var part = new GameObject($"Body {count}").transform;
                part.SetParent(blocker, false);
                part.gameObject.layer = layer;
                part.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                Vector3 meshScale = mf.transform.lossyScale;
                Vector3 parentScale = blocker.lossyScale;
                part.localScale = new Vector3(meshScale.x / parentScale.x, meshScale.y / parentScale.y, meshScale.z / parentScale.z);

                var collider = part.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
                collider.convex = true;
                count++;
            }

            if (count == 0)
            {
                sb.AppendLine($"  !! {root.name}: 차체 메시를 못 찾음, 건너뜀");
                return false;
            }

            // 차는 트랜스폼으로 움직인다. 리지드바디 없는 콜라이더를 매 프레임 옮기면 정적 콜라이더를 옮기는 셈이라,
            // 키네마틱으로 두어 플레이어를 자연스럽게 밀어내게 한다. 이 오브젝트에만 달아 차의 다른 콜라이더를 묶지 않는다.
            // 에디터의 GetComponent는 없을 때 가짜 null을 돌려줄 수 있어 ??를 쓰지 않는다.
            var rb = blocker.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = blocker.gameObject.AddComponent<Rigidbody>();
            }

            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;

            sb.AppendLine($"  {root.name}: 차체 메시 콜라이더 {count}개");
            return true;
        }

        /// <summary>이름이 <see cref="LayerName"/>인 레이어가 없으면 빈 칸(8번부터)에 만든다.</summary>
        private static int EnsureLayer(StringBuilder sb)
        {
            int layer = LayerMask.NameToLayer(LayerName);
            if (layer >= 0)
            {
                return layer;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = LayerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    sb.AppendLine($"  레이어 {LayerName}을(를) {i}번에 만듦");
                    return i;
                }
            }

            return -1;
        }

        /// <summary>충돌 행렬에서 layer 줄과 칸을 other만 켜고 모두 끈다. 프로젝트 설정 파일(DynamicsManager)에 바로 저장되게 SerializedObject로 고친다.</summary>
        private static void SetCollisionOnlyWith(int layer, int other)
        {
            var dynamics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0]);
            SerializedProperty matrix = dynamics.FindProperty("m_LayerCollisionMatrix");

            for (int i = 0; i < 32; i++)
            {
                bool collide = i == other;
                SetBit(matrix.GetArrayElementAtIndex(layer), i, collide);
                SetBit(matrix.GetArrayElementAtIndex(i), layer, collide);
            }

            dynamics.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static void SetBit(SerializedProperty row, int bit, bool value)
        {
            uint mask = 1u << bit;
            row.uintValue = value ? row.uintValue | mask : row.uintValue & ~mask;
        }
    }
}
