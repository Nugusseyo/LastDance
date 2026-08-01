using UnityEngine;

namespace _Works.CJW.Scripts.Player.FSM
{
    [CreateAssetMenu(fileName = "StateMachine data", menuName = "JW/Agent/StateMachine data", order = 0)]
    public class StateMachineSO : ScriptableObject
    {
        public string className;
        public int layer;
        public StateListSO stateList;
    }
}
