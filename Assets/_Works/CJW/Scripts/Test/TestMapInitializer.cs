using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;

namespace _Works.CJW.Scripts.Test
{
    /// <summary>
    /// 맵이 제대로 채워졌는지 확인만 한다.
    /// 등록은 MapPosition이 OnEnable에서 스스로 하므로 여기서 손으로 넣을 것이 없다.
    /// 다만 자리가 하나도 없으면 방문이 아예 시작되지 않는데,
    /// 그 사실을 늦게 알면 원인을 찾기 어렵다.
    /// </summary>
    public class TestMapInitializer : MonoBehaviour
    {
        [SerializeField] private MapDataSo mapDataSo;

        [Tooltip("최소 하나는 있어야 한다고 보는 지점 종류.")]
        [SerializeField] private MapPointType[] requiredPoints = { MapPointType.ParkingSlot };

        // MapPosition들의 OnEnable이 모두 지난 뒤에 세야 하므로 Awake가 아니라 Start에서 확인한다.
        private void Start()
        {
            if (mapDataSo == null)
            {
                Debug.LogError("[TestMapInitializer] MapData를 지정해야 합니다.", this);
                return;
            }

            if (requiredPoints == null)
            {
                return;
            }

            for (int i = 0; i < requiredPoints.Length; i++)
            {
                MapPointType type = requiredPoints[i];
                int count = mapDataSo.CountOf(type);

                if (count == 0)
                {
                    Debug.LogError(
                        $"[TestMapInitializer] {type} 지점이 하나도 없습니다. " +
                        "씬에 MapPosition을 놓고 MapData를 물렸는지 확인하세요.", this);
                    continue;
                }

                Debug.Log($"[TestMapInitializer] {type} {count}개 등록됨.", this);
            }
        }
    }
}