using System;
using System.IO;
using _Works.JYG._Scripts.Data_Container.Money;
using UnityEngine;

namespace _Works.JYG._Scripts.UI
{
    public class GameExit : MonoBehaviour
    {
        private const string SaveFilePath = "storeData.json";  //저장할 json데이터의 이름
        public string SavePath => Path.Combine(Application.persistentDataPath, SaveFilePath);  //저장 루트 (윈도우나 apk가 지정해주는 폴더임)

        [SerializeField] private IntegerDataContainer review;
        [SerializeField] private IntegerDataContainer money;
        
        public void ExitGame()
        {
            Debug.Log("게임종료a");
#if UNITY_EDITOR
            // 에디터에선 Application.Quit이 아무 일도 안 해서 플레이 모드를 끈다.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        
        public void ClearSaveFile()
        {
            Debug.Log("Try clear file");
            PlayerPrefs.DeleteAll();
            review.Value = 0;
            money.Value = 0;
            try
            {
                if (File.Exists(SavePath))
                {
                    File.Delete(SavePath);
                    Debug.Log("<color=yellow>상점 세이브 파일 삭제 완료:</color> " + SavePath);
                }
                else
                {
                    Debug.Log("삭제할 세이브 파일이 존재하지 않습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("세이브 파일 삭제 실패: " + e.Message);
            }
        }
    }
}
