using System.Collections.Generic;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Appearance
{
    /// <summary>손님이 스폰될 때마다 <see cref="CustomerLookSetSO"/>의 캐릭터 중 하나로 갈아입힌다.
    /// visual의 뼈대(Root)·Animator는 그대로 두고 SkinnedMeshRenderer의 메시·머티리얼·블렌드셰이프만 바꿔 끼우고 뼈를 이름으로 다시 잇는다 —
    /// 래그돌이 뼈에 붙여 둔 강체·관절과, 다른 모듈이 잡아 둔 Animator가 그대로 살아 있게 하기 위해서다.
    /// 프리팹에 원래 있던 렌더러도 갈아입을 자리로 재사용하고, 모자라면 새로 만들며, 남는 자리는 끈다.</summary>
    public class CustomerAppearanceModule : AbstractModule, ICustomerAppearance
    {
        [Tooltip("겉모습 후보 목록. 비우면 프리팹에 있는 모습 그대로 나온다.")]
        [SerializeField] private CustomerLookSetSO lookSet;

        public GameObject CurrentLook { get; private set; }

        private readonly Dictionary<string, Transform> _bones = new();
        private readonly List<SkinnedMeshRenderer> _slots = new();
        private readonly Dictionary<Renderer, Renderer> _slotOf = new();
        private Transform _visual;
        private LODGroup _lodGroup;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            // 손님은 Animator가 visual 자식에 있다. 뼈대와 렌더러는 그 아래에 있다.
            var animator = owner.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogError($"[{nameof(CustomerAppearanceModule)}] {owner.name}에 Animator가 없어 겉모습을 바꾸지 못합니다.", this);
                return;
            }

            _visual = animator.transform;
            _lodGroup = _visual.GetComponent<LODGroup>();

            Transform skeleton = _visual.Find("Root");
            if (skeleton == null)
            {
                Debug.LogError($"[{nameof(CustomerAppearanceModule)}] {owner.name}의 visual 아래에 Root 뼈대가 없습니다.", this);
                _visual = null;
                return;
            }

            // 뼈는 이름이 아니라 visual 기준 경로로 찾는다. 머리카락 프리팹이 Head 아래에 뼈대 사본(…/npc_haircut_*/Root/Hips/…)을
            // 통째로 들고 있어서, 이름으로 찾으면 계층 순서상 Neck 뒤에 오는 오른팔 뼈가 움직이지 않는 사본에 붙는다.
            // 사본의 뼈는 경로가 달라(…/npc_haircut_*/Root/…) 본 뼈대 경로와 겹치지 않는다.
            foreach (Transform bone in skeleton.GetComponentsInChildren<Transform>(true))
            {
                _bones.TryAdd(PathOf(bone, _visual), bone);
            }

            foreach (SkinnedMeshRenderer smr in _visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                _slots.Add(smr);
            }
        }

        public void Randomize()
        {
            if (_visual == null || lookSet == null)
            {
                return;
            }

            GameObject look = lookSet.Pick(CurrentLook);
            if (look != null)
            {
                Apply(look);
            }
        }

        private void Apply(GameObject look)
        {
            SkinnedMeshRenderer[] sources = look.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            _slotOf.Clear();

            for (int i = 0; i < sources.Length; i++)
            {
                SkinnedMeshRenderer slot = SlotAt(i);
                Copy(sources[i], slot);
                _slotOf[sources[i]] = slot;
            }

            // 이번 모습이 부위가 더 적으면 남는 자리를 끈다. 지우지 않고 다음 모습에서 다시 쓴다.
            for (int i = sources.Length; i < _slots.Count; i++)
            {
                _slots[i].gameObject.SetActive(false);
            }

            CopyLods(look.GetComponent<LODGroup>());
            CurrentLook = look;
        }

        private SkinnedMeshRenderer SlotAt(int index)
        {
            if (index < _slots.Count)
            {
                return _slots[index];
            }

            var go = new GameObject("look part");
            go.transform.SetParent(_visual, false);
            var slot = go.AddComponent<SkinnedMeshRenderer>();
            _slots.Add(slot);
            return slot;
        }

        private void Copy(SkinnedMeshRenderer source, SkinnedMeshRenderer slot)
        {
            slot.gameObject.name = source.name;
            slot.gameObject.SetActive(source.gameObject.activeSelf);

            // 후보의 뼈를 같은 경로의 우리 뼈로 바꿔 잇는다. 부위마다 뼈 순서·개수가 달라 매번 새로 짠다.
            Transform lookRoot = source.transform.root;
            Transform[] sourceBones = source.bones;
            var bones = new Transform[sourceBones.Length];
            for (int i = 0; i < sourceBones.Length; i++)
            {
                bones[i] = MatchBone(sourceBones[i], lookRoot);
            }

            slot.sharedMesh = source.sharedMesh;
            slot.bones = bones;
            slot.rootBone = MatchBone(source.rootBone, lookRoot);
            slot.sharedMaterials = source.sharedMaterials;
            slot.localBounds = source.localBounds;
            slot.quality = source.quality;
            slot.updateWhenOffscreen = source.updateWhenOffscreen;
            slot.shadowCastingMode = source.shadowCastingMode;
            slot.receiveShadows = source.receiveShadows;

            // 앞 모습의 블렌드셰이프 값이 남지 않게 새 메시의 것을 모두 덮어쓴다.
            int blendCount = source.sharedMesh != null ? source.sharedMesh.blendShapeCount : 0;
            for (int i = 0; i < blendCount; i++)
            {
                slot.SetBlendShapeWeight(i, source.GetBlendShapeWeight(i));
            }
        }

        private Transform MatchBone(Transform sourceBone, Transform lookRoot)
        {
            if (sourceBone == null)
            {
                return null;
            }

            // 머리카락 메시는 자기 뼈대 사본(…/Head/npc_haircut_*/Root/Hips/…)에 묶여 있다.
            // 사본 안의 경로(마지막 Root부터)를 본 뼈대에서 찾아 이으면 머리를 그대로 따라간다.
            string path = PathOf(sourceBone, lookRoot);
            int nested = path.LastIndexOf("/Root/", System.StringComparison.Ordinal);
            if (nested >= 0)
            {
                path = path.Substring(nested + 1);
            }
            else if (path.EndsWith("/Root", System.StringComparison.Ordinal))
            {
                path = "Root";
            }

            if (!_bones.TryGetValue(path, out Transform bone))
            {
                Debug.LogWarning($"[{nameof(CustomerAppearanceModule)}] {_owner.name}에 {lookRoot.name}의 뼈 {path}가 없습니다.", this);
            }

            return bone;
        }

        private static string PathOf(Transform t, Transform root)
        {
            string path = t.name;
            while (t.parent != null && t.parent != root)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        /// <summary>후보의 LOD 단계를 그대로 옮긴다. 렌더러만 우리 자리로 바꿔 끼운다.</summary>
        private void CopyLods(LODGroup source)
        {
            if (_lodGroup == null)
            {
                return;
            }

            if (source == null)
            {
                // 후보에 LOD가 없으면 모든 부위를 늘 보이게 한 단계로 묶는다.
                _lodGroup.SetLODs(new[] { new LOD(0f, _slots.ToArray()) });
                _lodGroup.RecalculateBounds();
                return;
            }

            LOD[] lods = source.GetLODs();
            for (int i = 0; i < lods.Length; i++)
            {
                Renderer[] from = lods[i].renderers;
                var to = new List<Renderer>(from.Length);
                foreach (Renderer r in from)
                {
                    if (r != null && _slotOf.TryGetValue(r, out Renderer slot))
                    {
                        to.Add(slot);
                    }
                }

                lods[i].renderers = to.ToArray();
            }

            _lodGroup.fadeMode = source.fadeMode;
            _lodGroup.animateCrossFading = source.animateCrossFading;
            _lodGroup.SetLODs(lods);
            _lodGroup.localReferencePoint = source.localReferencePoint;
            _lodGroup.size = source.size;
        }
    }
}
