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

        private Coroutine _speechCoroutine;

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
                    if (list.Any(x => x.Item1 == data.index))      //대본 index가 이미 엑셀 파일에 있으면 경고해준다.
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

        public void InitializeBubble(HumanType humanType, int index)   //버블을 소환하고싶으면 해당 함수를 호출해라.
        {
            _ended = false;

            if (tmp == null)
            {
                Debug.LogError("말풍선의 tmp가 지정되지 않아 Initialize가 불가능합니다. 취소 됨.");
                EndSpeech();
                return;
            }

            if (!humanDBs.TryGetValue(humanType, out var list) || list == null || list.Count == 0)
            {
                Debug.LogWarning($"해당 human상태에 맞는 데이터가 존재하지 않습니다. : {humanType}");
                EndSpeech();
                return;
            }

            // 전달받은 index와 일치하는 데이터를 찾고, 없을 경우 0번 인덱스의 데이터를 기본값으로 사용
            HumanData data = list.FirstOrDefault(x => x.Item1 == index).Item2 ?? list[0].Item2;
            List<string> stringList = data.GetStrings();   //content들을 모두 리스트화 시킴.

            if (stringList == null || stringList.Count == 0)
            {
                Debug.LogWarning($"[SpeechBubble] 출력할 대사 데이터가 비어있습니다. : {humanType}, Index: {index}");
                EndSpeech();
                return;
            }

            if (_speechCoroutine != null)
            {
                StopCoroutine(_speechCoroutine);
            }

            _speechCoroutine = StartCoroutine(CoShowSpeechProcess(data, stringList));
        }

        private IEnumerator CoShowSpeechProcess(HumanData data, List<string> stringList)
        {
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, data.delayTime));
            int totalCount = stringList.Count;

            switch (data.printType)
            {
                case BubbleType.InOrder:
                    for (int i = 0; i < totalCount; i++)
                    {
                        tmp.text = stringList[i]; //출력
                        yield return wait;
                    }
                    break;

                case BubbleType.Random:
                    for (int i = 0; i < totalCount; i++)
                    {
                        int randIndex = UnityEngine.Random.Range(0, stringList.Count); //랜덤 대사 인덱스 정하기
                        tmp.text = stringList[randIndex]; //출력
                        yield return wait;
                    }
                    break;

                default:
                    tmp.text = stringList[0]; //출력
                    yield return wait;
                    break;
            }

            _speechCoroutine = null;
            EndSpeech();
        }

        //대사를 띄우지 못했을 때도 기다리는 쪽이 멈추지 않도록 끝났다고 알리고 풀로 돌아간다.
        //말하던 손님의 요구가 풀리거나 손님이 사라지면 밖에서도 불러 대사를 바로 접는다. 이미 접혔으면 아무것도 하지 않는다.
        public void EndSpeech()
        {
            if (_ended)
                return;

            _ended = true;
            OnSpeechEnd?.Invoke();
            poolManager.Push(this);
        }

        private bool _ended;

        private void OnDisable()
        {
            if (_speechCoroutine != null)
            {
                StopCoroutine(_speechCoroutine);
                _speechCoroutine = null;
            }
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
            if (_speechCoroutine != null)
            {
                StopCoroutine(_speechCoroutine);
                _speechCoroutine = null;
            }
            tmp.text = "";
        }
        #endregion
    }
}