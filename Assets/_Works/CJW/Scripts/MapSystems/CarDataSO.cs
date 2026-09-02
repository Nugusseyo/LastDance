using _Works.CJW.Scripts.Customers.Data;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>
    /// 차 한 종류의 설정. 예전에는 VisitDirector가 모든 차에 같은 값을 먹였지만,
    /// 이제 하차 간격·이동 속도·손님 종류·차체 색을 차마다 따로 정할 수 있다.
    /// 태울 인원은 여기 없다. 좌석은 프리팹의 구조이므로 Car가 자기 좌석 수에서 직접 뽑는다.
    /// </summary>
    [CreateAssetMenu(fileName = "Car Data", menuName = "JW/Customers/Car Data", order = 0)]
    public class CarDataSO : ScriptableObject
    {
        [Header("풀")]
        [Tooltip("이 차를 꺼낼 풀 항목. 프리팹에는 Car가 붙어 있어야 한다.")]
        [SerializeField] private PoolItemSO poolItem;

        [Header("탑승")]
        [Tooltip("손님을 한 명씩 태우고 내릴 때의 간격(초).")]
        [SerializeField] private float boardingInterval = 0.4f;

        [Header("이동")]
        [Tooltip("0보다 크면 이동 모듈의 속도를 이 값으로 덮어쓴다.")]
        [SerializeField] private float moveSpeed = 6f;
        [Tooltip("정차 지점에 얼마나 가까워지면 도착으로 볼지.")]
        [SerializeField] private float arriveThreshold = 0.5f;

        [Header("손님")]
        [Tooltip("이 차에 탈 수 있는 손님 종류. 비워두면 VisitDirector의 기본 손님 목록을 쓴다.")]
        [SerializeField] private CustomerDataSO[] customers;

        [Header("외형")]
        [Tooltip("차체 색 후보. 스폰할 때 이 중 하나를 무작위로 고른다. " +
                 "비워두면 프리팹 머티리얼 색을 그대로 쓴다. 메시가 다른 차는 이게 아니라 프리팹을 나눠야 한다.")]
        [SerializeField] private Color[] bodyColors;

        [Header("스폰")]
        [Tooltip("여러 차 중 하나를 뽑을 때의 가중치. 0이면 뽑히지 않는다.")]
        [SerializeField, Min(0f)] private float spawnWeight = 1f;

        public PoolItemSO PoolItem => poolItem;
        public float BoardingInterval => boardingInterval;
        public float MoveSpeed => moveSpeed;
        public float ArriveThreshold => arriveThreshold;
        public float SpawnWeight => spawnWeight;

        /// <summary>비어 있으면 null을 돌려준다. 호출한 쪽이 기본 목록으로 넘어가면 된다.</summary>
        public CustomerDataSO[] Customers => customers != null && customers.Length > 0 ? customers : null;

        /// <summary>비어 있으면 null. 호출한 쪽이 프리팹 색을 그대로 두면 된다.</summary>
        public Color[] BodyColors => bodyColors != null && bodyColors.Length > 0 ? bodyColors : null;
    }
}
