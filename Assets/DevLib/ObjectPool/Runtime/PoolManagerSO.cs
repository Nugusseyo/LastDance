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
                IPoolable poolable = item.prefab.GetComponent<IPoolable>();
                Debug.Assert(poolable != null, $"Poolable component not found: {item.prefab.name}");
                
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