using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "EnemyStunned", story: "[Enemy] Stunned", category: "Action/Navigation", id: "6bb6d344dd1ce9318788dc8b81c3ed42")]
    public partial class EnemyStunnedAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        protected override Status OnStart()
        {
            if (Enemy.Value == null ||
                Enemy.Value.EnemyNavMeshAgent == null || Enemy.Value.Renderer == null)
                return Status.Failure;

            Enemy.Value.EnemyNavMeshAgent.StopImmediately();
            Enemy.Value.Renderer.Animator.speed = 0f;

            return Status.Success;
        }
    }
}