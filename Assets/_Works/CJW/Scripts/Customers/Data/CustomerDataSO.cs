using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>
    /// 손님 한 종류의 설정. 어떤 프리팹을 꺼낼지와 그 손님의 개별 수치를 함께 들고 있어서,
    /// 매니저를 건드리지 않고 에셋만 새로 만들어 손님 종류를 늘릴 수 있다.
    /// </summary>
    [CreateAssetMenu(fileName = "Customer Data", menuName = "JW/Customers/Customer Data", order = 1)]
    public class CustomerDataSO : ScriptableObject
    {
        [Header("풀")]
        [Tooltip("이 손님을 꺼낼 풀 항목. 프리팹에는 AbstractCustomer가 붙어 있어야 한다.")]
        [SerializeField] private PoolItemSO poolItem;

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

        public PoolItemSO PoolItem => poolItem;
        public float MoveSpeed => moveSpeed;
        public float AngularSpeed => angularSpeed;
        public float StoppingDistance => stoppingDistance;
        public float SpawnWeight => spawnWeight;
    }
}
