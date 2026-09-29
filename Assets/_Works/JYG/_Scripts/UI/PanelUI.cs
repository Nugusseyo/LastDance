using System;
using _Works.JJH._02_Scripts.Agents.Players;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace _Works.JYG._Scripts.UI
{
    public class PanelUI : MonoBehaviour
    {
        [SerializeField] private PlayerInputSO playerInputSO;
        [SerializeField] private UIInputSO uiInputSO;
        
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

            if(uiInputSO != null)
                Open.AddListener(HandleEscapeKey);
        }

        private void HandleEscapeKey()
        {
            uiInputSO.OnEscapePressed += HandleEscapeKeyPressed;
        }

        private void HandleEscapeKeyPressed()
        {
            uiInputSO.OnEscapePressed -= HandleEscapeKeyPressed;
            InvokeClose();
        }

        private void OnDestroy()
        {
            Open.RemoveListener(HandleOpenUI);
            Close.RemoveListener(HandleCloseUI);
            
            if(uiInputSO != null)
                Open.RemoveListener(HandleEscapeKey);
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

            if (uiInputSO != null && playerInputSO != null)
            {
                uiInputSO.SetEnable(active);
                playerInputSO.SetEnable(!active);
            }
        }
        
        [ContextMenu("Open")]
        public void InvokeOpen() => Open?.Invoke();
        
        [ContextMenu("Close")]
        public void InvokeClose() => Close?.Invoke();
    }
}
