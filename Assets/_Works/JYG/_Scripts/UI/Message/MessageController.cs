using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.Message
{
    public class MessageController : MonoBehaviour
    {
        [SerializeField] private EventChannelSO eventChannelSO;
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI noticeText;

        [Header("DOTween Settings")]
        [SerializeField] private float moveDistance = 120f; // 위로 이동할 Y축 거리
        [SerializeField] private float duration = 1.2f;     // 전체 연출 시간
        [SerializeField] private float fadeDuration = 0.4f; // 페이드 아웃되는 시간
        [SerializeField] private Ease moveEase = Ease.OutCubic; // 천천히 감속하며 올라가는 이징

        private Vector2 _startAnchoredPos;
        private Sequence _sequence;

        private void Awake()
        {
            _startAnchoredPos = rectTransform.anchoredPosition;

            canvasGroup.alpha = 0;
            if(eventChannelSO != null)
                eventChannelSO.AddListener<MessageEvent>(HandleInvokeMessage);
        }
        
        public void ShowNotice(string message)
        {
            KillTween();
            
            if (noticeText != null) noticeText.text = message;
            rectTransform.anchoredPosition = _startAnchoredPos;
            canvasGroup.alpha = 1f;
            gameObject.SetActive(true);
            
            _sequence = DOTween.Sequence();
            _sequence.Append(rectTransform.DOAnchorPosY(_startAnchoredPos.y + moveDistance, duration).SetEase(moveEase));
            
            float fadeStartTime = Mathf.Max(0f, duration - fadeDuration);
            _sequence.Insert(fadeStartTime, canvasGroup.DOFade(0f, fadeDuration));
            
            _sequence.OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
            
            _sequence.SetUpdate(true);
        }

        private void KillTween()
        {
            if (_sequence != null && _sequence.IsActive())
            {
                _sequence.Kill();
                _sequence = null;
            }
            rectTransform.DOKill();
            canvasGroup.DOKill();
        }

        private void OnDisable()
        {
            KillTween();
        }

        private void OnDestroy()
        {
            KillTween();
            
            if(eventChannelSO != null)
                eventChannelSO.RemoveListener<MessageEvent>(HandleInvokeMessage);
        }

        public void HandleInvokeMessage(MessageEvent evt)
            => ShowNotice(evt.Message);
        
        #if UNITY_EDITOR
        [ContextMenu("TestMethod")]
        public void TestMethod()
        {
            ShowNotice("돈이 부족합니다!");
        }
        #endif
    }
}
