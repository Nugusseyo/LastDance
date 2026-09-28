using UnityEngine;

namespace _Works.KDH._01.Scripts.DayNight
{
    public class NightLightGroup : MonoBehaviour
    {
        private Light[] nightLights;
        private float[] fullIntensities;

        private void Awake()
        {
            nightLights = GetComponentsInChildren<Light>(true);
            fullIntensities = new float[nightLights.Length];

            for (int i = 0; i < nightLights.Length; i++)
            {
                fullIntensities[i] = nightLights[i].intensity;
            }
        }

        private void Update()
        {
            float nightAmount = NightLighting.NightAmount;

            for (int i = 0; i < nightLights.Length; i++)
            {
                nightLights[i].intensity = fullIntensities[i] * nightAmount;
                nightLights[i].enabled = nightAmount > 0.01f;
            }
        }
    }
}
