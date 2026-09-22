using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace _Works.JYG._Scripts.UI
{
    public class PanelUI : MonoBehaviour
    {
        [SerializeField] private float fadeTime = 0.3f;
        protected CanvasGroup canvasGroup;

        public UnityEvent Open;
        public UnityEvent Close;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            Debug.Assert(canvasGroup != null, $"CanvasGroup이 Null입니다! GameObject : " + gameObject.name);
            SetActive(false, 0);
        }

        private void Start()
        {
            Open.AddListener(HandleOpenUI);
            Close.AddListener(HandleCloseUI);
        }

        private void OnDestroy()
        {
            Open.RemoveListener(HandleOpenUI);
            Close.RemoveListener(HandleCloseUI);
        }
        protected virtual void HandleOpenUI()
        {
            SetActive(true, fadeTime);
        }
        
        [ContextMenu("Close")]
        protected virtual void HandleCloseUI()
        {
            SetActive(false, fadeTime);
        }

        protected void SetActive(bool active, float duration)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(active ? 1f : 0, duration);
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }
        
        [ContextMenu("Open")]
        public void InvokeOpen() => Open?.Invoke();
        
        [ContextMenu("Close")]
        public void InvokeClose() => Close?.Invoke();
    }
}
