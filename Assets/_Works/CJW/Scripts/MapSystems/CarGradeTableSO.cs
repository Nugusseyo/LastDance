using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>평판별 차 등급 해금과 등장 가중치.
    /// 등급 X의 등장 확률 = X의 가중치 ÷ (해금된 등급들의 가중치 합).</summary>
    [CreateAssetMenu(fileName = "Car Grade Table", menuName = "JW/Customers/Car Grade Table", order = 1)]
    public class CarGradeTableSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public CarGrade grade;
            [Tooltip("평판이 이 값 이상이면 해금된다.")]
            public int unlockReputation;
            [Min(0f)] public float weight;
        }

        [SerializeField] private Entry[] entries =
        {
            new Entry { grade = CarGrade.Low, unlockReputation = 0, weight = 11f },
            new Entry { grade = CarGrade.Mid, unlockReputation = 35, weight = 7f },
            new Entry { grade = CarGrade.High, unlockReputation = 75, weight = 6f },
            new Entry { grade = CarGrade.Super, unlockReputation = 120, weight = 5f },
        };

        private readonly List<Entry> _pickBuffer = new();

        /// <summary>해금됐고 뽑을 차가 있는 등급 중에서 가중치로 하나 고른다. 고를 등급이 없으면 false.</summary>
        public bool TryPickGrade(int reputation, Func<CarGrade, bool> hasCandidate, out CarGrade grade)
        {
            _pickBuffer.Clear();

            for (int i = 0; i < entries.Length; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.grade == CarGrade.None || entry.weight <= 0f)
                {
                    continue;
                }

                if (reputation < entry.unlockReputation || !hasCandidate(entry.grade))
                {
                    continue;
                }

                _pickBuffer.Add(entry);
            }

            Entry picked = WeightedPicker.Pick(_pickBuffer, e => e.weight);
            grade = picked?.grade ?? CarGrade.None;
            return picked != null;
        }
    }
}
