using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "StopImmediately", story: "[Enemy] Stop Immediately", category: "Action/Physics", id: "9096d3bcffc1ca9a26b5438af2e9d9d9")]
    public partial class StopImmediatelyAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.EnemyNavMeshAgent == null)
                return Status.Failure;

            Enemy.Value.EnemyNavMeshAgent.StopImmediately();

            return Status.Success;
        }
    }
}

