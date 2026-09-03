using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>가중치를 보고 하나를 뽑는다. 가중치가 전부 0이면 균등하게 뽑는다.</summary>
    public static class WeightedPicker
    {
        public static T Pick<T>(IReadOnlyList<T> candidates, Func<T, float> weightOf) where T : class
        {
            if (candidates == null || candidates.Count == 0)
            {
                return null;
            }

            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                if (candidate != null)
                {
                    total += Mathf.Max(0f, weightOf(candidate));
                }
            }

            if (total <= 0f)
            {
                return PickAny(candidates);
            }

            float roll = Random.value * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[i];
                if (candidate == null)
                {
                    continue;
                }

                roll -= Mathf.Max(0f, weightOf(candidate));
                if (roll <= 0f)
                {
                    return candidate;
                }
            }

            return PickAny(candidates);
        }

        private static T PickAny<T>(IReadOnlyList<T> candidates) where T : class
        {
            // 가중치가 전부 0이거나 부동소수 오차로 위 루프가 빈손일 때. 아무 위치에서 시작해 한 바퀴 돈다.
            int start = Random.Range(0, candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                T candidate = candidates[(start + i) % candidates.Count];
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
