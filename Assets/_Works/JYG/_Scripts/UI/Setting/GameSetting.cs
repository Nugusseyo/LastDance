using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace _Works.JYG._Scripts.UI.Setting
{
    public class GameSetting : MonoBehaviour
    {
        [Header("UI Controls")]
        [SerializeField] private Slider masterVolume;      // 0 ~ 100
        [SerializeField] private Slider bgmVolume;         // 0 ~ 100
        [SerializeField] private Slider sfxVolume;         // 0 ~ 100
        [SerializeField] private Toggle isBlockAlarm;
        [SerializeField] private Slider mouseSensitivity; // 1 ~ 100

        [Header("Settings & Audio")]
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private SettingSO settingSO;

        // AudioMixer에 등록한 Exposed Parameter 이름 지정
        private const string MASTER_PARAM = "Master";
        private const string BGM_PARAM = "BGM";
        private const string SFX_PARAM = "SFX";

        private void Start()
        {
            // 저장된 설정 데이터 불러오기 및 UI / 시스템 초기화
            LoadSettings();

            // UI 슬라이더 및 토글에 이벤트 리스너 연결
            masterVolume.onValueChanged.AddListener(SetMasterVolume);
            bgmVolume.onValueChanged.AddListener(SetBGMVolume);
            sfxVolume.onValueChanged.AddListener(SetSFXVolume);
            isBlockAlarm.onValueChanged.AddListener(SetBlockAlarm);
            mouseSensitivity.onValueChanged.AddListener(SetMouseSensitivity);
        }

        #region Sound Control Logic

        public void SetMasterVolume(float value)
        {
            if (settingSO != null) settingSO.masterVolume = value;
            ApplyVolumeToMixer(MASTER_PARAM, value);
        }

        public void SetBGMVolume(float value)
        {
            if (settingSO != null) settingSO.bgmVolume = value;
            ApplyVolumeToMixer(BGM_PARAM, value);
        }

        public void SetSFXVolume(float value)
        {
            if (settingSO != null) settingSO.sfxVolume = value;
            ApplyVolumeToMixer(SFX_PARAM, value);
        }

        /// <summary>
        /// 0~100의 슬라이더 값을 AudioMixer의 데시벨(-80dB ~ 0dB) 범위로 변환하여 적용
        /// </summary>
        private void ApplyVolumeToMixer(string parameterName, float sliderValue)
        {
            if (audioMixer == null) return;

            // 0~100 값을 0.0001~1.0 비율로 변환 (0일 때 Log10 계산 오류 방지)
            float normalizedValue = Mathf.Clamp(sliderValue / 100f, 0.0001f, 1f);
            
            // 로그 스케일 적용 (20 * log10(비율)): 100 -> 0dB, 1 -> -40dB, 0.0001 -> -80dB(최저음)
            float dB = Mathf.Log10(normalizedValue) * 20f;

            audioMixer.SetFloat(parameterName, dB);
        }

        #endregion

        #region Other Settings Logic

        public void SetBlockAlarm(bool isBlocked)
        {
            if (settingSO != null) settingSO.isAlarmBanned = isBlocked;
        }

        public void SetMouseSensitivity(float value)
        {
            if (settingSO != null) settingSO.mouseSensitivity = value;
        }

        #endregion

        #region Save & Load

        /// <summary>
        /// 저장된 SettingSO 데이터를 불러와 UI와 AudioMixer에 반영
        /// </summary>
        private void LoadSettings()
        {
            if (settingSO == null) return;

            // 1. UI 값 반영 (Event 가해짐 방지를 위해 SetValueWithoutNotify 사용)
            masterVolume.SetValueWithoutNotify(settingSO.masterVolume);
            bgmVolume.SetValueWithoutNotify(settingSO.bgmVolume);
            sfxVolume.SetValueWithoutNotify(settingSO.sfxVolume);
            isBlockAlarm.SetIsOnWithoutNotify(settingSO.isAlarmBanned);
            mouseSensitivity.SetValueWithoutNotify(settingSO.mouseSensitivity);

            // 2. AudioMixer에 저장된 음량 수치 즉시 적용
            ApplyVolumeToMixer(MASTER_PARAM, settingSO.masterVolume);
            ApplyVolumeToMixer(BGM_PARAM, settingSO.bgmVolume);
            ApplyVolumeToMixer(SFX_PARAM, settingSO.sfxVolume);
        }

        #endregion
    }
}