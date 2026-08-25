using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace _Works.JYG._Scripts.UI
{
	public class GaugeUI : MonoBehaviour
	{
		[SerializeField] private Image targetImage;				// FillAmount를 조정할 UI
		[SerializeField] private EventChannelSO eventChannel;	// GameEvent로 float값 넘겨주면서 할당받게 해야 함.
		
		[Tooltip("Value가 Max(1)인 경우, 자동으로 사라지는 UI인가? (true면 자동으로 소멸)")]
		[SerializeField] private bool isAutoHide = true;
		[SerializeField] private CanvasGroup canvasGroup; //AutoHide시 필수로 할당 필요.
		[SerializeField] private float disappearDelay = 0.7f;

		private bool isDoFade = false;

		private void Awake()
		{
			eventChannel.AddListener<GaugeEvent>(HandleGaugeChanged);
			
			if(isAutoHide && canvasGroup == null)
				Debug.LogWarning($"Auto Hide UI의 경우, CanvasGroup이 할당되어야 정상 작동 합니다! : {gameObject.name}");
		}
	
		private void OnDestroy()
		{
			eventChannel.RemoveListener<GaugeEvent>(HandleGaugeChanged);
		}

		private void HandleGaugeChanged(GaugeEvent evt)
		{
			targetImage.fillAmount = evt.Value;

			if (!isAutoHide) return;	//Auto Hiding 아님.
			if (canvasGroup == null) return; //할당 실수한 UI 담당 잘못. Awake에서 안내해줌.

			//AutoHiding일 시
			if (!Mathf.Approximately(evt.Value, 1))
			{
				canvasGroup.alpha = 1;
				isDoFade = false;
				canvasGroup.DOKill();
			}
			else if (!isDoFade && canvasGroup.alpha != 0)
			{
				isDoFade = true;
				canvasGroup.DOFade(0, disappearDelay).SetEase(Ease.OutQuart)
					.OnComplete(() => SetDOFade(false));
			}
		}
		
		private void SetDOFade(bool isFade) 
			=> isDoFade = isFade;
	}
}
