using System;
using UnityEngine;
using UnityEngine.UI;

namespace _Works.JYG._Scripts.UI.GuestUI
{
    public class WorldGauge : MonoBehaviour
    {
        [SerializeField] private Image fillImage;
        [SerializeField] private Gradient gradient;
        private float _value;

        public float NormalizedValue
        {
            get => _value;
            set
            {
                float clampedValue = Mathf.Clamp01(value);
                if (fillImage != null)
                {
                    fillImage.fillAmount = clampedValue;
                    fillImage.color = gradient.Evaluate(clampedValue);
                }
                _value = clampedValue;
            }
        }
    }
}
