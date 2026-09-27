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

        [Tooltip("켜면 이 데이터의 poolItem 대신, 평판으로 등급을 뽑아 등급 있는 차들 중 하나의 겉모습으로 나온다. " +
                 "손님·방문 연출·속도는 이 데이터를 그대로 쓴다. 뽑을 차가 없으면 poolItem으로 나온다.")]
        [SerializeField] private bool randomVisual;

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
        [Tooltip("평판에 따라 해금되는 등급. 등급을 먼저 뽑고, 같은 등급 안에서 spawnWeight로 나눈다. None이면 등급 추첨에서 빠진다.")]
        [SerializeField] private CarGrade grade;

        [Tooltip("같은 등급 안에서 하나를 뽑을 때의 가중치. 0이면 뽑히지 않는다.")]
        [SerializeField, Min(0f)] private float spawnWeight = 1f;
        
        [SerializeField, Min(0)] private int maxConcurrent;
        
        [Header("판매")]
        [SerializeField] private int price;

        [Header("주유")]
        [Tooltip("이 차에 주유를 해 줬을 때의 정가. 손님의 지불 배율(CustomerDataSO.fuelPayRate)을 곱해 RefuelingEvent로 보내고, " +
                 "평판·상점 보너스는 MoneyManager가 곱한다.")]
        [SerializeField, Min(0)] private int fuelPrice = 50;

        public PoolItemSO PoolItem => poolItem;
        public bool RandomVisual => randomVisual;

        /// <summary>동시에 나올 수 있는 최대 대수. 0이면 제한 없음.</summary>
        public int MaxConcurrent => maxConcurrent;
        public float BoardingInterval => boardingInterval;
        public float MoveSpeed => moveSpeed;
        public float ArriveThreshold => arriveThreshold;
        public float SpawnWeight => spawnWeight;
        public CarGrade Grade => grade;
        public int Price => price;
        public int FuelPrice => fuelPrice;

        /// <summary>비어 있으면 null을 돌려준다. 호출한 쪽이 기본 목록으로 넘어가면 된다.</summary>
        public CustomerDataSO[] Customers => customers != null && customers.Length > 0 ? customers : null;

        /// <summary>비어 있으면 null. 호출한 쪽이 기본 연출을 그대로 쓰면 된다.</summary>
        public VisitState[] StateOverrides =>
            stateOverrides != null && stateOverrides.Length > 0 ? stateOverrides : null;
    }
}
