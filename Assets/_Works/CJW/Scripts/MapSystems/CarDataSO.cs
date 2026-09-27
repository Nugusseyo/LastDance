using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.Customers.Visit;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.MapSystems
{
    [CreateAssetMenu(fileName = "Car Data", menuName = "JW/Customers/Car Data", order = 0)]
    public class CarDataSO : ScriptableObject
    {
        [Header("풀")]
        [SerializeField] private PoolItemSO poolItem;

        [Header("탑승")]
        [SerializeField] private float boardingInterval = 0.4f;

        [Header("이동")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float arriveThreshold = 0.5f;

        [Header("손님")]
        [SerializeField] private CustomerDataSO[] customers;
        
        [Header("방문 연출")]
        [SerializeReference] private VisitState[] stateOverrides;

        [Header("스폰")]
        [Tooltip("여러 차 중 하나를 뽑을 때의 가중치. 0이면 뽑히지 않는다.")]
        [SerializeField, Min(0f)] private float spawnWeight = 1f;
        
        [SerializeField, Min(0)] private int maxConcurrent;
        
        [Header("판매")]
        [SerializeField] private int price;

        public PoolItemSO PoolItem => poolItem;

        /// <summary>동시에 나올 수 있는 최대 대수. 0이면 제한 없음.</summary>
        public int MaxConcurrent => maxConcurrent;
        public float BoardingInterval => boardingInterval;
        public float MoveSpeed => moveSpeed;
        public float ArriveThreshold => arriveThreshold;
        public float SpawnWeight => spawnWeight;
        public int Price => price;

        /// <summary>비어 있으면 null을 돌려준다. 호출한 쪽이 기본 목록으로 넘어가면 된다.</summary>
        public CustomerDataSO[] Customers => customers != null && customers.Length > 0 ? customers : null;

        /// <summary>비어 있으면 null. 호출한 쪽이 기본 연출을 그대로 쓰면 된다.</summary>
        public VisitState[] StateOverrides =>
            stateOverrides != null && stateOverrides.Length > 0 ? stateOverrides : null;
    }
}
