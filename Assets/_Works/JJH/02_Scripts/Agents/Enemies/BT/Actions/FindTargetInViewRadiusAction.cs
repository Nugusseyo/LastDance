using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "FindTargetInViewRadius", story: "Find [Target] In [Enemy] ViewRadius", category: "Action/Find", id: "49c776d2829631aafc0b757bf650c6e1")]
    public partial class FindTargetInViewRadiusAction : Action
    {
        [SerializeReference] public BlackboardVariable<GameObject> Target;
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;

        private ISensor _sensor;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Sensor == null)
                return Status.Failure;

            _sensor = Enemy.Value.Sensor;

            if (_sensor.IsTargetInViewRadius(10, out Collider hitCollider))
            {
                Target.Value = hitCollider.gameObject;
                return Status.Success;
            }

            return Status.Failure;
        }
    }
}