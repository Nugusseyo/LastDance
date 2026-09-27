using System;
using UnityEngine;
using UnityEngine.UI;

namespace _Works.JYG._Scripts.UI.GuestUI
{
    [RequireComponent(typeof(Canvas))]
    public class WorldGauge : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Gradient gradient;
        private float _value;
        private Canvas _canvas;

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();
        }

        public float NormalizedValue
        {
            get => _value;
            set
            {
                float clampedValue = Mathf.Clamp01(value);
                if (Mathf.Approximately(_value, clampedValue))
                    return;
                
                if (fillImage != null)
                {
                    fillImage.fillAmount = clampedValue;
                    fillImage.color = gradient.Evaluate(clampedValue);
                }
                _value = clampedValue;
                
                _canvas.enabled = !Mathf.Approximately(clampedValue, 1);
            }
        }
    }
}
