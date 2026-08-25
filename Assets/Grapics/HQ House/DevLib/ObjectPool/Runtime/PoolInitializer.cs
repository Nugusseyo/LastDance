using UnityEngine;

namespace DevLib.ObjectPool.Runtime
{
    public class PoolInitializer : MonoBehaviour
    {
        [field: SerializeField] public PoolManagerSO PoolManager { get;  private set; }

        private void Awake()
        {
            
            PoolInitializer[] initializers = FindObjectsByType<PoolInitializer>(FindObjectsSortMode.None);
            if (initializers.Length > 1)
            {
                Destroy(gameObject);
                return;
            }
            
            DontDestroyOnLoad(gameObject);
            PoolManager.InitializePool(transform); //이 모노 비해비어가 풀매니저를 초기화해준다.
        }
        
        public T Pop<T>(PoolItemSO type) where T : IPoolable => PoolManager.Pop<T>(type);
        public void Push(IPoolable item) => PoolManager.Push(item);
    }
}