using System.Collections.Generic;
using UnityEngine;

namespace DevLib.ObjectPool.Runtime
{
    [CreateAssetMenu(fileName = "PoolManager", menuName = "Lib/Object Pool/PoolManager", order = 0)]
    public class PoolManagerSO : ScriptableObject
    {
        public List<PoolItemSO> itemList = new();

        private Dictionary<PoolItemSO, Pool> _pools;
        private Transform _rootTrm;

        public void InitializePool(Transform rootTrm)
        {
            _rootTrm = rootTrm;
            _pools = new Dictionary<PoolItemSO, Pool>();

            foreach (PoolItemSO item in itemList)
            {
                // 항목 하나가 비어 있다고 예외로 끊기면 뒤의 풀이 전부 만들어지지 않는다. 건너뛰고 이름을 남긴다.
                if (item == null)
                {
                    Debug.LogError($"[PoolManager] {name}의 itemList에 비어 있는 항목이 있어 건너뜁니다.", this);
                    continue;
                }

                if (item.prefab == null)
                {
                    Debug.LogError($"[PoolManager] {item.name}의 prefab이 비어 있어 건너뜁니다.", item);
                    continue;
                }

                if (_pools.ContainsKey(item))
                {
                    Debug.LogError($"[PoolManager] {item.name}이(가) itemList에 두 번 들어 있어 건너뜁니다.", item);
                    continue;
                }

                IPoolable poolable = item.prefab.GetComponent<IPoolable>();
                if (poolable == null)
                {
                    Debug.LogError($"[PoolManager] {item.prefab.name}에 IPoolable 컴포넌트가 없어 풀을 만들지 않습니다.", item.prefab);
                    continue;
                }

                Pool pool = new Pool(item, _rootTrm, item.initCount);
                _pools.Add(item, pool);
            }
        }

        public T Pop<T>(PoolItemSO type) where T : IPoolable
        {
            Debug.Assert(_rootTrm != null, "오브젝트 풀을 사용하기 전에 반드시 초기화 되어 있어야 합니다.");

            if (_pools.TryGetValue(type, out Pool pool))
            {
                return (T)pool.Pop();
            }

            return default;
        }

        public void Push(IPoolable item)
        {
            Debug.Assert(_rootTrm != null, "오브젝트 풀을 사용하기 전에 반드시 초기화 되어 있어야 합니다.");
            if (_pools.TryGetValue(item.PoolItem, out Pool pool))
            {
                pool.Push(item);
            }
        }
    }
}