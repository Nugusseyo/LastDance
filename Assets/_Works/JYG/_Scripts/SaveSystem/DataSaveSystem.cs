using UnityEngine;

namespace _Works.JYG._Scripts.SaveSystem
{
    public static class DataSaveSystem
    {
        public static T GetSaveData<T>(string key)
        {
            string rawData = PlayerPrefs.GetString(key);
            return JsonUtility.FromJson<T>(rawData);
        }


        public static void SetSaveData(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
        }
    }
}