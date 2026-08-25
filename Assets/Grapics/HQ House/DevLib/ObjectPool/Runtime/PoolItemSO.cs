using UnityEngine;

namespace DevLib.ObjectPool.Runtime
{
    [CreateAssetMenu(fileName = "Pool Item", menuName = "Lib/Object Pool/Pool Item", order = 10)]
    public class PoolItemSO : ScriptableObject
    {
        public string poolingName;
        public GameObject prefab;
        public int initCount;
        
        private void OnValidate()
        {
            if (prefab != null && !prefab.TryGetComponent(out IPoolable poolable))
            {
                Debug.LogError($"풀링 프리팹에는 IPoolable인터페이스를 구현한 스크립트가 적어도 1개 이상 있어야 합니다.");
                prefab = null;
            }
        }
    }
}