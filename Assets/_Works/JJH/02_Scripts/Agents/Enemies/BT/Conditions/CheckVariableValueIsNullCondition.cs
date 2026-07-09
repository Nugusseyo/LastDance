using System;
using Unity.Behavior;
using UnityEngine;

namespace JJH._02_Scripts.Agents.Enemies.BT.Conditions
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "CheckVariableValueIsNull", story: "[Value] Is Null", category: "Variable Conditions", id: "1b2aaa195194a862cec8e726195cff17")]
    public partial class CheckVariableValueIsNullCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Value;

        public override bool IsTrue()
        {
            if (Value.Value == null)
                return true;
            else
                return false;
        }
    }
}