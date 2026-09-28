using System;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Works.JYG._Scripts.Vending_Machine
{
    [RequireComponent(typeof(CanvasGroup))]
    public class VendingTooltipViewer : MonoBehaviour
    {
        public EventChannelSO eventChannel;
        private CanvasGroup _canvasGroup;
        
        [Header("DOTween Settings")]
        [SerializeField] private float duration = 0.5f;

        [SerializeField] private Vector2 offset;

        [Header("TMP Fields")]
        [SerializeField] private TextMeshProUGUI nameTmp;
        [SerializeField] private TextMeshProUGUI contentTmp;

        private RectTransform RectTrm => transform as RectTransform;

        private void Awake()
        {
            if (eventChannel != null)
            {
                eventChannel.AddListener<TooltipEvent>(HandleTooltipEvent);
                eventChannel.AddListener<TipMoveEvent>(HandleTooltipMove);
            }
            
            _canvasGroup = GetComponent<CanvasGroup>();
            
            
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (eventChannel != null)
            {
                eventChannel.RemoveListener<TooltipEvent>(HandleTooltipEvent);
                eventChannel.RemoveListener<TipMoveEvent>(HandleTooltipMove);
            }
            _canvasGroup.DOKill();
        }

        private void HandleTooltipEvent(TooltipEvent evt)
        {
            _canvasGroup.DOKill();
            
            if (evt.Active)
            {
                if (nameTmp != null) nameTmp.text = evt.ItemName;
                if (contentTmp != null) contentTmp.text = evt.Content;
                
                _canvasGroup.DOFade(1f, duration).SetUpdate(true);
            }
            else
            {
                _canvasGroup.DOFade(0f, duration).SetUpdate(true);
            }
        }
        
        private void HandleTooltipMove(TipMoveEvent evt)
        {
            RectTrm.position = evt.Position + offset;
        }
    }
}
