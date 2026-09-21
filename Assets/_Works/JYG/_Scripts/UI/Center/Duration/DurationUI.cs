using System;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Works.JYG._Scripts.UI.Center.Duration
{
    public class DurationUI : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI timerTmp;
        [SerializeField] private EventChannelSO eventChannelSO;
        
        private void Awake()
        {
            if (fillImage == null || timerTmp == null || eventChannelSO == null)
            {
                Debug.LogWarning("필수 값들이 누락되어있습니다.");
                return;
            }
            fillImage.fillAmount = 0;
            timerTmp.text = "";
            fillImage.enabled = false;
            timerTmp.enabled = false;
            
            eventChannelSO.AddListener<DurationEvent>(HandleDurationEvent);
        }

        private void HandleDurationEvent(DurationEvent evt)
        {
            if (Mathf.Approximately(evt.CurDuration, evt.MaxDuration))
            {
                fillImage.fillAmount = 0;
                timerTmp.text = "";
                fillImage.enabled = false;
                timerTmp.enabled = false;
                return;
            }
            float normalizedTime = Mathf.Clamp01(evt.CurDuration / evt.MaxDuration);
            
            
            fillImage.enabled = true;
            timerTmp.enabled = true;
            
            fillImage.fillAmount = normalizedTime;
            timerTmp.text = $"{(evt.CurDuration):0.#}s";
        }
    }
}
