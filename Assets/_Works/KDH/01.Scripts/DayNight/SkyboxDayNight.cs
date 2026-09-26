using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.KDH._01.Scripts.DayNight
{
    public class SkyboxDayNight : MonoBehaviour
    {
        [SerializeField] private DayAndNightControl dayAndNight;
        [SerializeField] private Material dawnSkybox;
        [SerializeField] private Material daySkybox;
        [SerializeField] private Material sunsetSkybox;
        [SerializeField] private Material nightSkybox;
        [SerializeField, Range(0f, 1f)] private float dawnTime = 0.2f;
        [SerializeField, Range(0f, 1f)] private float dayStartTime = 0.3f;
        [SerializeField, Range(0f, 1f)] private float sunsetTime = 0.7f;
        [SerializeField, Range(0f, 1f)] private float nightStartTime = 0.8f;
        [SerializeField, Range(0.1f, 10f)] private float fadeDuration = 3f;
        [SerializeField, Range(0f, 1f)] private float fadeLowestBrightness = 0.6f;
        [SerializeField, Range(0f, 5f)] private float cloudSpeed = 0.5f;
        [SerializeField] private Material fadeableSkyboxTemplate;

        private readonly Dictionary<Material, float> baseExposures = new Dictionary<Material, float>();
        private Material dawnSky;
        private Material daySky;
        private Material sunsetSky;
        private Material nightSky;
        private Material currentSkybox;
        private Coroutine fadeRoutine;
        private float cloudRotation;

        private void Awake()
        {
            dawnSky = CopySkybox(dawnSkybox);
            daySky = CopySkybox(daySkybox);
            sunsetSky = CopySkybox(sunsetSkybox);
            nightSky = CopySkybox(nightSkybox);
        }

        private void Update()
        {
            MoveClouds();

            Material targetSkybox = GetSkybox(dayAndNight.currentTime);
            if (targetSkybox == currentSkybox) return;

            if (currentSkybox == null)
            {
                currentSkybox = targetSkybox;
                ChangeSkybox(targetSkybox);
                return;
            }

            Material previousSkybox = currentSkybox;
            currentSkybox = targetSkybox;

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                ResetExposure(previousSkybox);
            }

            fadeRoutine = StartCoroutine(FadeRoutine(previousSkybox, targetSkybox));
        }

        private IEnumerator FadeRoutine(Material from, Material to)
        {
            float halfDuration = fadeDuration * 0.5f;

            float fromLowest = baseExposures[from] * fadeLowestBrightness;
            float toLowest = baseExposures[to] * fadeLowestBrightness;

            yield return FadeExposure(from, baseExposures[from], fromLowest, halfDuration);

            ResetExposure(from);
            SetExposure(to, toLowest);
            ChangeSkybox(to);

            yield return FadeExposure(to, toLowest, baseExposures[to], halfDuration);

            fadeRoutine = null;
        }

        private IEnumerator FadeExposure(Material sky, float start, float end, float duration)
        {
            float time = 0f;

            while (time < duration)
            {
                time += Time.deltaTime;
                SetExposure(sky, Mathf.Lerp(start, end, time / duration));
                yield return null;
            }

            SetExposure(sky, end);
        }

        private Material GetSkybox(float time)
        {
            if (time < dawnTime || time >= nightStartTime) return nightSky;
            if (time < dayStartTime) return dawnSky;
            if (time >= sunsetTime) return sunsetSky;

            return daySky;
        }

        private void MoveClouds()
        {
            cloudRotation = (cloudRotation + cloudSpeed * Time.deltaTime) % 360f;

            foreach (Material sky in baseExposures.Keys)
            {
                sky.SetFloat("_Rotation", cloudRotation);
            }
        }

        private Material CopySkybox(Material original)
        {
            Material copy = new Material(original);

            if (!copy.HasProperty("_Exposure"))
            {
                copy.shader = fadeableSkyboxTemplate.shader;
            }

            baseExposures[copy] = copy.GetFloat("_Exposure");
            return copy;
        }

        private void SetExposure(Material sky, float exposure)
        {
            sky.SetFloat("_Exposure", exposure);
        }

        private void ResetExposure(Material sky)
        {
            SetExposure(sky, baseExposures[sky]);
        }

        private void ChangeSkybox(Material sky)
        {
            RenderSettings.skybox = sky;
            DynamicGI.UpdateEnvironment();
        }
    }
}
