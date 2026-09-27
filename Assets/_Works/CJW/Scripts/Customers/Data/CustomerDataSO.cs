using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>손님 한 종류의 설정. 어떤 프리팹을 꺼낼지와 개별 수치를 함께 들고 있어 에셋만 새로 만들어 종류를 늘릴 수 있다.</summary>
    [CreateAssetMenu(fileName = "Customer Data", menuName = "JW/Customers/Customer Data", order = 1)]
    public class CustomerDataSO : ScriptableObject
    {
        [Header("풀")]
        [Tooltip("이 손님을 꺼낼 풀 항목. 프리팹에는 AbstractCustomer가 붙어 있어야 한다.")]
        [SerializeField] private PoolItemSO poolItem;

        [Header("손님 타입")]
        [field: SerializeField] public CustomerType CustomerType { get; private set; }

        [Header("이동")]
        [Tooltip("0보다 크면 NavMeshAgent의 speed를 이 값으로 덮어쓴다.")]
        [SerializeField] private float moveSpeed = 3.5f;
        [Tooltip("0보다 크면 NavMeshAgent의 angularSpeed를 이 값으로 덮어쓴다.")]
        [SerializeField] private float angularSpeed = 120f;
        [Tooltip("목적지에 얼마나 가까워지면 도착으로 볼지.")]
        [SerializeField] private float stoppingDistance = 0.2f;

        [Header("스폰")]
        [Tooltip("여러 손님 중 하나를 뽑을 때의 가중치. 0이면 뽑히지 않는다.")]
        [SerializeField, Min(0f)] private float spawnWeight = 1f;

        [Tooltip("켜면 이 손님은 늘 혼자 차를 타고 온다. 차를 버리고 떠나는 손님처럼 동승자가 남겨지면 안 되는 종류에 쓴다.")]
        [SerializeField] private bool ridesAlone;

        [Tooltip("켜면 이 손님을 태운 차 바로 뒤에 같은 종류 손님을 태운 차가 한 대 더 온다. 싸움처럼 다른 차의 상대가 꼭 있어야 하는 행동에 쓴다. 뒤따를 차의 자리가 없으면 뽑히지 않는다.")]
        [SerializeField] private bool comesInPairs;

        [Header("정산")]
        [Tooltip("주유를 해 줬을 때 받는 돈의 배율. 1이면 정가, 네고 손님처럼 값을 깎는 손님은 1보다 작게 둔다.")]
        [SerializeField, Range(0f, 1f)] private float fuelPayRate = 1f;

        public PoolItemSO PoolItem => poolItem;
        public float MoveSpeed => moveSpeed;
        public float AngularSpeed => angularSpeed;
        public float StoppingDistance => stoppingDistance;
        public float SpawnWeight => spawnWeight;
        public bool RidesAlone => ridesAlone;
        public bool ComesInPairs => comesInPairs;
        public float FuelPayRate => fuelPayRate;

        /// <summary>정가 <paramref name="basePrice"/>의 주유를 해 줬을 때 이 손님에게 실제로 받는 돈.
        /// 주유 정산을 하는 쪽은 정가를 그대로 더하지 말고 이 값을 더한다.</summary>
        public int FuelPayment(int basePrice) => Mathf.RoundToInt(basePrice * fuelPayRate);

        /// <summary>이 손님이 주유를 원하는지. 타입 이름이 아니라 프리팹의 행동(주유 상태·주유 요구)을 보고 정한다.
        /// 베이스 프리팹의 주유 상태를 물려받은 변형도 놓치지 않기 위해서다. 차를 채울 때 몇 번 부를 뿐이라 따로 캐시하지 않는다.</summary>
        public bool WantsFuel
        {
            get
            {
                GameObject prefab = poolItem != null ? poolItem.prefab : null;
                CustomerFSMModule fsm = prefab != null ? prefab.GetComponentInChildren<CustomerFSMModule>(true) : null;
                return fsm != null && fsm.WantsFuel;
            }
        }
    }
}
