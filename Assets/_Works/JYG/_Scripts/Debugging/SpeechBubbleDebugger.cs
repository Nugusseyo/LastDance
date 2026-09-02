using System;
using System.Collections;
using _Works.JYG._Scripts.UI.SpeechBubble;
using DevLib.ObjectPool.Runtime;
using Resources.DataBase.Human_Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.JYG._Scripts.Debugging
{
    public class SpeechBubbleDebugger : MonoBehaviour
    {
        [SerializeField] private PoolManagerSO poolManager;
        [SerializeField] private PoolItemSO item;
        [SerializeField] private HumanType type;
        [SerializeField] private float destroyTime = 2f;

        private void Update()
        {
            if (Keyboard.current.yKey.wasPressedThisFrame)
            {
                SpeechBubble bubble = poolManager.Pop<SpeechBubble>(item);
                bubble.InitializeBubble(type);
                //StartCoroutine(DestroyBubble(2f, bubble));
            }
        }

        private IEnumerator DestroyBubble(float t, SpeechBubble bubble) //InitializeBubble에서 구현해준다.
        {
            yield return new WaitForSeconds(t);
            poolManager.Push(bubble);
        }
    }
}
