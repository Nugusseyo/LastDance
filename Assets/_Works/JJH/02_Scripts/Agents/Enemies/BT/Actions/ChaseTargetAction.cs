using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "ChaseTarget", story: "[Enemy] chase [Target]", category: "Action/Navigation", id: "75a14125fe2bdeb7a86ba4b65b837b0b")]
    public partial class ChaseTargetAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<GameObject> Target;

        [Header("Chase Setting")]
        [SerializeField] private float viewRadius = 10f;
        [SerializeField] private float destinationUpdateDistance = 0.2f;

        private ISensor _sensor;
        private INavMeshAgent _navMeshAgent;

        private Vector3 _targetPos;
        private Vector3 _lastTargetPos;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Sensor == null ||
                Enemy.Value.EnemyNavMeshAgent == null || Target.Value == null)
                return Status.Failure;

            _navMeshAgent = Enemy.Value.EnemyNavMeshAgent;
            _sensor = Enemy.Value.Sensor;

            _targetPos = Target.Value.transform.position;
            _lastTargetPos = _targetPos;

            _navMeshAgent.MoveTo(_targetPos);

            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            if (Target.Value == null)
                return Status.Failure;

            bool isInRadius = _sensor.IsTargetInViewRadius(viewRadius, out Collider hitCollider);

            if (!isInRadius)
            {
                _navMeshAgent.StopImmediately();
                return Status.Failure;
            }

            _targetPos = Target.Value.transform.position;

            if (Vector3.Distance(_lastTargetPos, _targetPos) >= destinationUpdateDistance)
            {
                _lastTargetPos = _targetPos;
                _navMeshAgent.MoveTo(_targetPos);
            }

            return Status.Running;
        }

        protected override void OnEnd()
        {
            _navMeshAgent.StopImmediately();
        }
    }
}