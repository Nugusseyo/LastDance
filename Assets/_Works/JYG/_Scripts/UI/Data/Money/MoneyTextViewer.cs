using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.Data.Money
{
    public class MoneyTextViewer : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI moneyTmp;
        [SerializeField] private Color greenColor = Color.green;
        [SerializeField] private Color redColor = Color.red;
        private Transform goalObject;
        private Transform parent;

        [Header("DOTween Setting")] 
        [SerializeField] private float duration = 2f;
        [SerializeField] private Ease ease = Ease.InOutQuint;
        
        public RectTransform RectTrm => transform as RectTransform;

        public void InitializeViewer(Transform goal)
        {
            goalObject = goal;
        }

        public void SetText(string text, bool isGreen)
        {
            moneyTmp.color = isGreen ? greenColor : redColor;
            moneyTmp.SetText(text);
        }
        
        public void MoveToGoal(Action onComplete)
        {
            RectTrm.DOKill();
            moneyTmp.alpha = 1;
            
            Sequence seq = DOTween.Sequence();
            seq.Append(
                RectTrm.DOMove(goalObject.position, duration)
                    .SetEase(ease)
                    .SetUpdate(true)
                    .OnComplete(() => onComplete?.Invoke()));
            seq.Join(moneyTmp.DOFade(0, duration).SetEase(ease));
        }
    }
}
