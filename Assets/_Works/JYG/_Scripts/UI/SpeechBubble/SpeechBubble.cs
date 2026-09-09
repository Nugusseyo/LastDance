using System;
using System.Collections;
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
        private HumanDB humanDB;    //손님들의 대사를 담는 Database
        private Dictionary<HumanType, List<(int, HumanData)>> humanDBs; //손님의 타입에 따른 대사를 출력하기 위해 리스트 튜플과 타입을 묶은 리스트
        [SerializeField] private TextMeshProUGUI tmp;                   //스피치 버블의 TMP
        
        [Header("Pooling")]
        [SerializeField] private PoolManagerSO poolManager; //스피치 버블을 풀링하기 위한 풀매니저 SO
        [SerializeField] private float destroyTime = 5f;    //몇 초 후에 사라지는가?
        private WaitForSeconds destroyWait;                 //WaitForSeconds를 캐싱해놓고 씀.
        public event Action OnSpeechEnd;    //SpeechBubble이 사라질 때 실행되는 Action

        private void Awake()
        {
            humanDB = UnityEngine.Resources.Load<HumanDB>("DataBase/Human Data/HumanDB");   //휴먼 데이터의 Resource를 들고온다. 경로가 틀리면 큰일 남.
            humanDBs = new Dictionary<HumanType, List<(int, HumanData)>>();                     //초기화 구간
            destroyWait = new WaitForSeconds(destroyTime);
            if (humanDB == null)
            {
                Debug.LogError("말풍선 데이터 손상됨. 기존 데이터의 위치 변경이 주요 원인일 가능성이 높음. \"Speech Bubble\" 코드 비활성화.");
                enabled = false;
                return;
            }

            foreach (HumanData data in humanDB.Sheet1)  //Sheet1에 있는 휴먼 대사들을 가져와 Dictionary에 넣는다. Sheet1이라는 이름은 나중에 고쳐야할듯.
            {
                if (humanDBs.TryGetValue(data.type, out List<(int, HumanData)> list))   //해당 타입에 대한 List가 있으면 들고 온다.
                {
                    if (list.Select(x => x.Item1 == data.index).Any())      //대본 index가 이미 엑셀 파일에 있으면 경고해준다.
                    {
                        Debug.LogWarning("같은 Type, 같은 Index의 데이터가 이미 존재합니다.\n" +
                                         "나중의 데이터가 덮어씌워져 적용 됩니다.");
                    }
                    list.Add((data.index, data));
                }
                else
                {
                    humanDBs.Add(data.type, new List<(int, HumanData)>());              //List가 존재하지 않아 새로 만든다.
                    humanDBs[data.type].Add((data.index, data));                        //데이터를 안에 넣어준다.
                }
                Debug.Log($"Added Data : {data.type}, {data.index}, {data.contents1}");
            }
        }

        public void InitializeBubble(HumanType humanType)   //버블을 소환하고싶으면 해당 함수를 호출해라.
        {
            if (tmp == null)
            {
                Debug.LogError("말풍선의 tmp가 지정되지 않아 Initialize가 불가능합니다. 취소 됨.");
                return;
            }

            var list = humanDBs[humanType];
            if (list == null)
            {
                Debug.LogWarning($"해당 human상태에 맞는 데이터가 존재하지 않습니다. : {humanType}");
                return;
            }
            List<string> stringList = list[0].Item2.GetStrings();   //지금은 Good손님, Bad손님으로 나눠서 무조건 0번 인덱스를 쓴다. 세부 진상 대사는 정우랑 얘기해서 추가해야함.
            Debug.Log(stringList.Count);
            int randIndex =  UnityEngine.Random.Range(0, stringList.Count); //랜덤 대사 인덱스 정하기
            tmp.text = stringList[randIndex];                               //출력

            StartCoroutine(DestroyBubble());                          //n초 후 사라지게 만드는 코루틴 실행
        }

        private IEnumerator DestroyBubble()
        {
            yield return destroyWait;
            OnSpeechEnd?.Invoke();
            poolManager.Push(this);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        #region PoolManager
        [field:SerializeField] public PoolItemSO PoolItem { get; set; }
        public GameObject GameObject => gameObject;
        public void ResetItem()
        {
            tmp.text = "";
        }
        #endregion
    }
}
