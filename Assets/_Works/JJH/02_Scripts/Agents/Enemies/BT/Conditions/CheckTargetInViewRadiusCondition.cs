using System;
using Unity.Behavior;
using UnityEngine;

namespace JJH._02_Scripts.Agents.Enemies.BT.Conditions
{
    [Serializable, Unity.Properties.GeneratePropertyBag]
    [Condition(name: "CheckTargetInViewRadius", story: "[Target] In [Enemy] ViewRadius", category: "Conditions", id: "8e703086747b364d2824be3f22bdf211")]
    public partial class CheckTargetInViewRadiusCondition : Condition
    {
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        public override bool IsTrue()
        {
            if (Enemy.Value == null || Enemy.Value.Sensor == null)
                return false;
            return Enemy.Value.Sensor.IsTargetInViewRadius(10, out Collider hitCollider);
        }
    }
}

