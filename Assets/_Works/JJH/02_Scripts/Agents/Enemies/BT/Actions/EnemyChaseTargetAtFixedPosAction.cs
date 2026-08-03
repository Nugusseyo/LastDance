using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "EnemyChaseTargetAtFixedPos", story: "[Enemy] chase [Target] at [FixedPos]", category: "Action/Navigation", id: "e326d2eaf2e7b249221058e87865b45a")]
    public partial class EnemyChaseTargetAtFixedPosAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<Vector3> FixedPos;
        private INavMeshAgent _navMeshAgent;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Target.Value == null ||
                Enemy.Value.Sensor == null || Enemy.Value.Renderer == null ||
                Enemy.Value.EnemyNavMeshAgent == null)
                return Status.Failure;

            _navMeshAgent = Enemy.Value.EnemyNavMeshAgent;

            _navMeshAgent.MoveToFixedPos(Target.Value.transform.position, FixedPos.Value);

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Target.Value == null)
                return Status.Failure;

            _navMeshAgent.MoveToFixedPos(Target.Value.transform.position, FixedPos.Value);

            return Status.Running;
        }
    }
}

