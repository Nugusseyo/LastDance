using System.Collections.Generic;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Appearance
{
    /// <summary>손님이 입을 수 있는 캐릭터 프리팹 목록. 모든 손님이 이 하나를 같이 써서 후보를 한 곳에서 늘리고 줄인다.
    /// 후보는 손님과 같은 뼈대(Root 아래 뼈 이름·위치가 같은 것)여야 한다 — 뼈는 그대로 두고 메시만 갈아 끼우기 때문이다.</summary>
    [CreateAssetMenu(fileName = "Customer Look Set", menuName = "JW/Customers/Customer Look Set", order = 2)]
    public class CustomerLookSetSO : ScriptableObject
    {
        [Tooltip("겉모습 후보. 각 프리팹의 SkinnedMeshRenderer와 LODGroup을 그대로 옮겨 입힌다.")]
        [SerializeField] private List<GameObject> looks = new();

        public IReadOnlyList<GameObject> Looks => looks;

        /// <summary>후보 중 하나. <paramref name="except"/>는 후보가 둘 이상일 때 피한다 — 같은 풀 객체가 연달아 같은 옷으로 나오지 않게.</summary>
        public GameObject Pick(GameObject except = null)
        {
            if (looks.Count == 0)
            {
                return null;
            }

            int skip = looks.Count > 1 ? looks.IndexOf(except) : -1;
            if (skip < 0)
            {
                return looks[Random.Range(0, looks.Count)];
            }

            // 피할 자리를 뺀 개수에서 뽑고, 그 자리 이후면 한 칸 민다.
            int index = Random.Range(0, looks.Count - 1);
            return looks[index >= skip ? index + 1 : index];
        }
    }
}
