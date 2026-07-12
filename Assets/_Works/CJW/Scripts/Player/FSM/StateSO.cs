using DevLib.AnimatorSystem;
using UnityEngine;

namespace Agents.FSM
{
    [CreateAssetMenu(fileName = "State data", menuName = "JW/Agent/State data", order = 0)]
    public class StateSO : ScriptableObject
    {
        public string stateName;
        public string className;
        public int assetIndex;
        public HashDataSO stateParam;
    }
}