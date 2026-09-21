using System;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.Conditions
{
    /// <summary>맵에 특정 지점이 몇 개 이상 있을 때만 통과한다. "주유구가 두 개 이상일 때만 싸운다" 같은 조건이 이것으로 표현된다.</summary>
    [Serializable]
    public sealed class MapPointCountCondition : StateCondition
    {
        [Tooltip("세어 볼 지점의 종류.")]
        [SerializeField] private MapPointType pointType = MapPointType.OilDispenser;

        [Tooltip("이 개수 이상이어야 통과한다.")]
        [SerializeField, Min(1)] private int minCount = 2;

        [Tooltip("켜면 지금 비어 있는 지점만 센다. 끄면 점유 여부와 무관하게 전부 센다.")]
        [SerializeField] private bool availableOnly;

        public override bool IsMet(CustomerContext ctx)
        {
            MapDataSo map = ctx?.MapData;

            if (map == null)
            {
                // 맵을 모르면 셀 수가 없다. 조용히 통과시키면 조건을 건 의미가 없으므로 막는다.
                Debug.LogWarning($"[{nameof(MapPointCountCondition)}] CustomerFSMModule에 MapData가 없어 조건을 만족하지 않은 것으로 봅니다.",
                    ctx?.Customer);
                return false;
            }

            int count = availableOnly ? map.AvailableCountOf(pointType) : map.CountOf(pointType);

            return count >= minCount;
        }
    }
}
