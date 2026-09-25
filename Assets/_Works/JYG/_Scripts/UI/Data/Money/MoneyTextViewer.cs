using System;
using System.Collections;
using DevLib.ObjectPool.Runtime;
using DG.Tweening;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using IPoolable = DevLib.ObjectPool.Runtime.IPoolable;

namespace _Works.JYG._Scripts.UI.Data.Money
{
    //TODO : 여기 구조 이상한거 바로잡기
    public class MoneyTextViewer : MonoBehaviour, IPoolable
    {
        [SerializeField] private TextMeshProUGUI moneyTmp;
        private Transform goalObject;
        [SerializeField] private Vector2 offset;
        [SerializeField] private Vector2 stPosOffset;
        private Transform parent;

        [Header("DOTween Setting")] 
        [SerializeField] private float duration = 2f;
        [SerializeField] private Ease ease = Ease.InOutQuint;

        private const string PLUS = "+";
        private const string MINUS = "-";

        private RectTransform goalRectTrm;
        
        public RectTransform RectTrm => transform as RectTransform;

        private void Awake()
        {
            if(goalObject != null)
                goalRectTrm = goalObject.transform as RectTransform;
        }

        public void MovingText(int value, Transform parent, Transform goal)
        {
            transform.SetParent(parent);
            goalObject = goal;
            goalRectTrm = goalObject.transform as RectTransform;
            
            string sign = value >= 0 ? PLUS : MINUS;
            moneyTmp.text = sign + value;
            RectTrm.DOKill();
            if (goalRectTrm == null) return;

            RectTrm.DOAnchorPos(goalRectTrm.anchoredPosition + offset, duration)
                .SetEase(ease)
                .SetUpdate(true);
        }

        [field:SerializeField] public PoolItemSO PoolItem { get; set; }
        public GameObject GameObject => gameObject;
        public void ResetItem()
        {
            RectTrm.DOKill();

            RectTrm.anchoredPosition = goalRectTrm.anchoredPosition + stPosOffset;
        }
    }
}
