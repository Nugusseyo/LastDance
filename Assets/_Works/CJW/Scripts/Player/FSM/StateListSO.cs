using Agents.FSM;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.FSM
{
    [CreateAssetMenu(fileName = "State list data", menuName = "JW/Agent/State list", order = 0)]
    public class StateListSO : ScriptableObject
    {
        [HideInInspector] public string generatePath;
        public string enumName;
        public StateSO[] states;
        public int layer;
    }
}