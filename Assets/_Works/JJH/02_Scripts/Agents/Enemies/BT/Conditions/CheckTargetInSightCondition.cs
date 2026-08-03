using JJH._02_Scripts.Agents.Enemies;
using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "CheckTargetInSight", story: "[Target] in [Enemy] sight", category: "Conditions", id: "0aacb8afd518a433b0c92ad394a63403")]
public partial class CheckTargetInSightCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

    public override bool IsTrue()
    {
        if (Enemy.Value == null || Enemy.Value.Sensor == null)
            return false;
        return Enemy.Value.Sensor.IsTargetInSight(90f, 10f);
    }
}
