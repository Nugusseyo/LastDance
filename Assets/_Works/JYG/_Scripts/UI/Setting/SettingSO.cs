using System;
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

        // 감도 변경 시 외부(카메라 등)로 알릴 이벤트
        public event Action<float> OnSensitivityChanged;

        private const string KEY_MASTER = "Setting_MasterVolume";
        private const string KEY_BGM = "Setting_BGMVolume";
        private const string KEY_SFX = "Setting_SFXVolume";
        private const string KEY_ALARM = "Setting_IsAlarmBanned";
        private const string KEY_SENSITIVITY = "Setting_MouseSensitivity";

        public void Load()
        {
            masterVolume = PlayerPrefs.GetFloat(KEY_MASTER, 100f);
            bgmVolume = PlayerPrefs.GetFloat(KEY_BGM, 100f);
            sfxVolume = PlayerPrefs.GetFloat(KEY_SFX, 100f);
            isAlarmBanned = PlayerPrefs.GetInt(KEY_ALARM, 0) == 1;
            mouseSensitivity = PlayerPrefs.GetFloat(KEY_SENSITIVITY, 50f);
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(KEY_MASTER, masterVolume);
            PlayerPrefs.SetFloat(KEY_BGM, bgmVolume);
            PlayerPrefs.SetFloat(KEY_SFX, sfxVolume);
            PlayerPrefs.SetInt(KEY_ALARM, isAlarmBanned ? 1 : 0);
            PlayerPrefs.SetFloat(KEY_SENSITIVITY, mouseSensitivity);
            PlayerPrefs.Save();

            // 설정 저장 시 감도 변경 이벤트 발행
            OnSensitivityChanged?.Invoke(mouseSensitivity);
        }
    }
}