using System.Collections.Generic;
using UnityEngine;

namespace DevLib.ObjectPool.Runtime
{
    public class Pool
    {
        private readonly Stack<IPoolable> _pool;
        private readonly Transform _parent;
        private readonly GameObject _prefab;

        public Pool(PoolItemSO poolItemSO, Transform parent, int initCount)
        {
            _parent = parent;
            _prefab = poolItemSO.prefab;
            _pool = new Stack<IPoolable>(initCount);

            for (int i = 0; i < initCount; i++)
            {
                GameObject gameObject = Object.Instantiate(_prefab, _parent);
                gameObject.SetActive(false);
                IPoolable poolable = gameObject.GetComponent<IPoolable>();
                Debug.Assert(poolable != null, $"Poolable component not found: {gameObject.name}");
                
                _pool.Push(poolable);
            }
        }

        public IPoolable Pop()
        {
            IPoolable item;
            if (_pool.Count == 0)
            {
                GameObject gameObject = Object.Instantiate(_prefab, _parent);
                item = gameObject.GetComponent<IPoolable>();
            }
            else
            {
                item = _pool.Pop();
                item.GameObject.SetActive(true);
            }
            item.ResetItem();
            return item;
        }

        public void Push(IPoolable item)
        {
            item.GameObject.SetActive(false);
            _pool.Push(item);
        }
    }
}