using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace _Works.KDH._01.Scripts.DayNight
{
    public class NightLighting : MonoBehaviour
    {
        [SerializeField] private Light sunLight;
        [SerializeField] private float nightExposure = -3f;
        [SerializeField] private Color nightColor = new Color(0.55f, 0.65f, 1f);
        [SerializeField, Range(-100f, 0f)] private float nightSaturation = -50f;
        [SerializeField, Range(0f, 1f)] private float nightReflection = 0.05f;
        [SerializeField] private float changeSpeed = 0.5f;
        [SerializeField] private float lampNightIntensity = 40f;
        [SerializeField] private float lampGlow = 1.5f;

        private readonly List<Light> lamps = new List<Light>();
        public static float NightAmount { get; private set; }

        private Volume nightVolume;
        private float maxSunIntensity;
        private float nightAmount;

        private void Awake()
        {
            maxSunIntensity = sunLight.intensity;
            nightVolume = CreateNightVolume();
        }

        private void Start()
        {
            Camera mainCamera = Camera.main;

            if (mainCamera != null)
            {
                mainCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            }

            FindLamps();
        }

        private void Update()
        {
            float daylight = maxSunIntensity > 0f ? Mathf.Clamp01(sunLight.intensity / maxSunIntensity) : 1f;
            nightAmount = Mathf.MoveTowards(nightAmount, 1f - daylight, changeSpeed * Time.deltaTime);

            NightAmount = nightAmount;
            nightVolume.weight = nightAmount;
            RenderSettings.reflectionIntensity = Mathf.Lerp(1f, nightReflection, nightAmount);

            foreach (Light lamp in lamps)
            {
                if (lamp == null) continue;

                lamp.intensity = lampNightIntensity * nightAmount;
                lamp.enabled = nightAmount > 0.01f;
            }
        }

        private void FindLamps()
        {
            foreach (Light sceneLight in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (sceneLight.type == LightType.Directional) continue;
                if (sceneLight.GetComponentInParent<NightLightGroup>() != null) continue;

                lamps.Add(CreateNightLamp(sceneLight));
            }
        }

        private Light CreateNightLamp(Light original)
        {
            GameObject lampObject = new GameObject("Night Lamp");
            lampObject.transform.SetParent(original.transform, false);

            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.spotAngle = 120f;
            lamp.range = original.range;
            lamp.color = original.color;
            lamp.shadows = LightShadows.None;
            lamp.intensity = 0f;
            lamp.enabled = false;

            return lamp;
        }

        private Volume CreateNightVolume()
        {
            GameObject volumeObject = new GameObject("Night Volume");
            volumeObject.transform.SetParent(transform);

            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;

            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.postExposure.Override(nightExposure);
            colorAdjustments.colorFilter.Override(nightColor);
            colorAdjustments.saturation.Override(nightSaturation);

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(lampGlow);
            bloom.threshold.Override(0.8f);
            bloom.scatter.Override(0.7f);

            volume.profile = profile;
            return volume;
        }
    }
}
