using UnityEngine;

namespace _Works.JYG._Scripts.SaveSystem
{
    public static class DataSaveSystem //저장 시스템을 간단히 하기 위한 Static class
    {
        public static T GetSaveData<T>(string key)      //Key를 주면, Json을 넘겨준다.
        {
            string rawData = PlayerPrefs.GetString(key);
            return JsonUtility.FromJson<T>(rawData);
        }


        public static void SetSaveData<T>(string key, T value)    //value와 key를 넘겨주면 PlayerPrefs에 등록해준다.
        {
            string data = JsonUtility.ToJson(value);
            PlayerPrefs.SetString(key, data);
        }
    }
}