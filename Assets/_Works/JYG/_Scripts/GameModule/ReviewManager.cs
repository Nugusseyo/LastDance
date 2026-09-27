using System;
using System.Collections;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.Events;
using _Works.JYG._Scripts.UI.Setting;
using DevLib.EventChannelSystem;
using Resources.DataBase.Review_Data;
using UnityEngine;
using UnityEngine.Events;

namespace _Works.JYG._Scripts.GameModule
{
    public class ReviewManager : MonoBehaviour
    {
        [SerializeField] private IntegerDataContainer reviewContainer;
        [SerializeField] private float increasePercentage = 0.5f;
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private SettingSO setting;

        private float _accumulatedValue = 0f;
        private Coroutine _reviewCoroutine;

        public UnityEvent<ReviewType, int> OnValueChanged; //lastValue : index

        private bool _isDestroy = false;

        [Header("ReviewValue")]
        [SerializeField] private int resGood = 5;

        [SerializeField] private int resBad = -3;
        [SerializeField] private int resLate = -1;

        
        #region Review Block & Buff logic

        private Coroutine _reviewBlockCoroutine;
        private float _duration;
        private bool _isBlocked = false;

        private Coroutine _reviewBuffCoroutine;
        private bool _isBuff = false;
        
        #endregion
        private void Awake()
        {
            if (reviewContainer == null)
                Debug.LogWarning("ReviewManager에 ReviewContainer가 존재하지 않습니다!");
            if (eventChannel != null)
            {
                eventChannel.AddListener<ReviewEvent>(PlusValue);
                eventChannel.AddListener<ReviewBlockEvent>(HandleReviewBlock);
                eventChannel.AddListener<ReviewBuffEvent>(HandleReviewBuff);
            }
        }

        private void HandleReviewBuff(ReviewBuffEvent evt)
        {
            if(_reviewBuffCoroutine != null)
                StopCoroutine(_reviewBuffCoroutine);
            
            _reviewBuffCoroutine = StartCoroutine(ReviewBuff(evt.Duration));
        }

        private IEnumerator ReviewBuff(float duration)
        {
            _isBuff = true;
            eventChannel.RaiseEvent(UIEvents.BuffEvent.Init(BuffType.Coke, duration));  //평점 2배는 콜라임.
            yield return new WaitForSeconds(duration);
            _isBuff = false;
            _reviewBuffCoroutine = null;
        }

        private void HandleReviewBlock(ReviewBlockEvent evt)
        {
            if (_reviewBlockCoroutine != null)
            {
                StopCoroutine(_reviewBlockCoroutine);
            }

            _reviewBlockCoroutine = StartCoroutine(ReviewBlockWithDuration(evt.BlockDuration));
        }

        private IEnumerator ReviewBlockWithDuration(float blockDuration)
        {
            _isBlocked = true;
            eventChannel.RaiseEvent(UIEvents.BuffEvent.Init(BuffType.Cider, blockDuration)); //평점 보호는 사이다임.
            
            yield return new WaitForSeconds(blockDuration);
            
            _isBlocked = false;
            _reviewBlockCoroutine = null;
        }

        private void OnEnable()
        {
            if (reviewContainer != null)
            {
                _isDestroy = false;
                _reviewCoroutine = StartCoroutine(CoIncreaseReviewProgress());
            }
        }

        private void OnDisable()
        {
            if (_reviewCoroutine != null)
            {
                _isDestroy = true;
                StopCoroutine(_reviewCoroutine);
                _reviewCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            if (eventChannel != null)
            {
                eventChannel.RemoveListener<ReviewEvent>(PlusValue);
                eventChannel.RemoveListener<ReviewBlockEvent>(HandleReviewBlock);
                eventChannel.RemoveListener<ReviewBuffEvent>(HandleReviewBuff);
            }
        }

        private IEnumerator CoIncreaseReviewProgress()
        {
            WaitForSeconds wait = new WaitForSeconds(1f);

            while (!_isDestroy)
            {
                yield return wait;
                
                _accumulatedValue += increasePercentage;
                
                if (_accumulatedValue >= 1f)
                {
                    int addValue = (int)_accumulatedValue;
                    reviewContainer.Value += addValue;
                    _accumulatedValue -= addValue;
                }
            }
        }

        private void PlusValue(ReviewEvent evt)
        {
            if (reviewContainer == null) return;
            
            bool canAlarm = !(evt.ReviewType == ReviewType.Good && setting.isAlarmBanned);
            
            if(canAlarm)
                OnValueChanged?.Invoke(evt.ReviewType, evt.Index);

            if (_isBlocked && evt.ReviewType != ReviewType.Good)
                return;
            
            int multiplier = _isBuff ? 2 : 1;
            
            reviewContainer.Value += evt.ReviewType switch
            {
                ReviewType.Good => resGood,
                ReviewType.Bad => resBad,
                ReviewType.Late => resLate,
                _ => 0
            } * multiplier;
        }
        
        #if UNITY_EDITOR
        
        [ContextMenu("AddValue")]
        public void AddValue()
        {
            if (eventChannel != null)
            {
                eventChannel.RaiseEvent(UIEvents.ReviewEvent.Review(1, ReviewType.Good));
            }
        }

        [ContextMenu("MinusValue")]
        public void MinusValue()
        {
            if (eventChannel != null)
            {
                eventChannel.RaiseEvent(UIEvents.ReviewEvent.Review(1, ReviewType.Bad));
            }
        }
            
        #endif
    }
}