using System;
using System.Collections;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.Events;
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

        private float _accumulatedValue = 0f;
        private Coroutine _reviewCoroutine;

        public UnityEvent<int, int, ReviewType> OnValueChanged; //FirstValue : newValue, lastValue : OldValue

        private bool _isDestroy = false;

        private void Awake()
        {
            if (reviewContainer == null)
                Debug.LogWarning("ReviewManager에 ReviewContainer가 존재하지 않습니다!");
            if (eventChannel != null)
                eventChannel.AddListener<ReviewEvent>(PlusValue);
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
            
            OnValueChanged?.Invoke(reviewContainer.Value + evt.PlusValue, reviewContainer.Value, evt.ReviewType);
            reviewContainer.Value += evt.PlusValue;
        }
        
        #if UNITY_EDITOR
        
        [ContextMenu("AddValue")]
        public void AddValue()
        {
            if (eventChannel != null)
            {
                eventChannel.RaiseEvent(UIEvents.ReviewEvent.IncreaseValue(10, ReviewType.Good));
            }
        }

        [ContextMenu("MinusValue")]
        public void MinusValue()
        {
            if (eventChannel != null)
            {
                eventChannel.RaiseEvent(UIEvents.ReviewEvent.IncreaseValue(-10, ReviewType.Bad));
            }
        }
            
        #endif
    }
}