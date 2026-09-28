using UnityEngine;

namespace _Works.JYG._Scripts.UI.Setting
{
    [CreateAssetMenu(fileName = "SettingSO", menuName = "Scriptable Objects/SettingSO")]
    public class SettingSO : ScriptableObject
    {
        [Header("Audio Settings")]
        public float masterVolume = 100f;
        public float bgmVolume = 100f;
        public float sfxVolume = 100f;

        [Header("Game Settings")]
        public bool isAlarmBanned = false;
        public float mouseSensitivity = 50f;

        // PlayerPrefs 저장 키 상수
        private const string KEY_MASTER = "Setting_MasterVolume";
        private const string KEY_BGM = "Setting_BGMVolume";
        private const string KEY_SFX = "Setting_SFXVolume";
        private const string KEY_ALARM = "Setting_IsAlarmBanned";
        private const string KEY_SENSITIVITY = "Setting_MouseSensitivity";

        /// <summary>
        /// 저장된 데이터를 불러옵니다. 키가 없으면 기본값을 사용합니다.
        /// </summary>
        public void Load()
        {
            masterVolume = PlayerPrefs.GetFloat(KEY_MASTER, 100f);
            bgmVolume = PlayerPrefs.GetFloat(KEY_BGM, 100f);
            sfxVolume = PlayerPrefs.GetFloat(KEY_SFX, 100f);
            isAlarmBanned = PlayerPrefs.GetInt(KEY_ALARM, 0) == 1;
            mouseSensitivity = PlayerPrefs.GetFloat(KEY_SENSITIVITY, 50f);
        }

        /// <summary>
        /// 현재 SO의 데이터를 PlayerPrefs에 저장합니다.
        /// </summary>
        public void Save()
        {
            PlayerPrefs.SetFloat(KEY_MASTER, masterVolume);
            PlayerPrefs.SetFloat(KEY_BGM, bgmVolume);
            PlayerPrefs.SetFloat(KEY_SFX, sfxVolume);
            PlayerPrefs.SetInt(KEY_ALARM, isAlarmBanned ? 1 : 0);
            PlayerPrefs.SetFloat(KEY_SENSITIVITY, mouseSensitivity);
            PlayerPrefs.Save();
        }
    }
}