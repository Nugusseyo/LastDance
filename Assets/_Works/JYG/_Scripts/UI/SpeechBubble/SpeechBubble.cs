using System.Collections.Generic;
using System.Linq;
using DevLib.ObjectPool.Runtime;
using Resources.DataBase.Human_Data;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.SpeechBubble
{
    public class SpeechBubble : MonoBehaviour, IPoolable
    {
        private HumanDB humanDB;
        private Dictionary<HumanType, List<(int, HumanData)>> humanDBs;
        [SerializeField] private TextMeshProUGUI tmp;
        [SerializeField] private RectTransform rect;

        private void Awake()
        {
            humanDB = UnityEngine.Resources.Load<HumanDB>("DataBase/Human Data/HumanDB");
            humanDBs = new Dictionary<HumanType, List<(int, HumanData)>>();
            if (humanDB == null)
            {
                Debug.LogError("말풍선 데이터 손상됨. 기존 데이터의 위치 변경이 주요 원인. \"Speech Bubble\" 코드 비활성화.");
                enabled = false;
                return;
            }

            foreach (HumanData data in humanDB.Sheet1)
            {
                if (humanDBs.TryGetValue(data.type, out List<(int, HumanData)> list))
                {
                    if (list.Select(x => x.Item1 == data.index).Any())
                    {
                        Debug.LogWarning("같은 Type, 같은 Index의 데이터가 이미 존재합니다.\n" +
                                         "나중의 데이터가 덮어씌워져 적용 됩니다.");
                    }
                    list.Add((data.index, data));
                }
                else
                {
                    humanDBs.Add(data.type, new List<(int, HumanData)>());
                    humanDBs[data.type].Add((data.index, data));
                }
                Debug.Log($"Added Data : {data.type}, {data.index}, {data.contents1}");
            }
        }

        public void InitializeBubble(HumanType humanType)
        {
            if (tmp == null || rect == null)
            {
                Debug.LogError("말풍선의 tmp또는 rect가 지정되지 않아 Initialize가 불가능합니다. 취소 됨.");
                return;
            }

            var list = humanDBs[humanType];
            if (list == null)
            {
                Debug.LogWarning($"해당 human상태에 맞는 데이터가 존재하지 않습니다. : {humanType}");
                return;
            }
            List<string> stringList = list[0].Item2.GetStrings();
            Debug.Log(stringList.Count);
            int randIndex =  UnityEngine.Random.Range(0, stringList.Count);
            tmp.text = stringList[randIndex];
        }

        [field:SerializeField] public PoolItemSO PoolItem { get; set; }
        public GameObject GameObject => gameObject;
        public void ResetItem()
        {
            tmp.text = "";
        }
    }
}
