using UnityEngine;

namespace _Works.KDH._01.Scripts.DayNight
{
    public class DayNightTimer : MonoBehaviour
    {
        [SerializeField] private DayAndNightControl dayAndNight;
        [SerializeField, Range(1f, 30f)] private float minutesPerChange = 5f;
        [SerializeField, Range(0.1f, 1f)] private float nightTimeSpeed = 0.5f;
        [SerializeField, Range(0f, 1f)] private float nightEndTime = 0.2f;
        [SerializeField, Range(0f, 1f)] private float nightStartTime = 0.8f;

        private void Awake()
        {
            dayAndNight.SecondsInAFullDay = minutesPerChange * 60f * 2f;
        }

        private void Update()
        {
            float time = dayAndNight.currentTime;
            bool isNight = time < nightEndTime || time >= nightStartTime;

            dayAndNight.timeMultiplier = isNight ? nightTimeSpeed : 1f;
        }
    }
}
