using UnityEngine;

namespace _Works.JYG._Scripts.UI.Setting
{
    [CreateAssetMenu(fileName = "SettingSO", menuName = "Scriptable Objects/SettingSO")]
    public class SettingSO : ScriptableObject
    {
        public bool isAlarmBanned = false;
    }
}
