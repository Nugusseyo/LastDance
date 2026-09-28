using UnityEngine;

namespace _Works.JYG._Scripts.UI
{
    public class GameExit : MonoBehaviour
    {
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
    }
}
