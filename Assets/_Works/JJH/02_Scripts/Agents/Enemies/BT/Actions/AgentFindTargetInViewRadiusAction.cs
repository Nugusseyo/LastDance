using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "AgentFindTargetInViewRadius", story: "Find [Target] In [Agent] ViewRadius", category: "Action/Find", id: "49c776d2829631aafc0b757bf650c6e1")]
    public partial class FindTargetInViewRadiusAction : Action
    {
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<Agent> Agent;
        private ISensor _sensor;

        protected override Status OnStart()
        {
            if (Agent.Value == null || Agent.Value.Sensor == null)
                return Status.Failure;

            _sensor = Agent.Value.Sensor;

            if (_sensor.IsTargetInViewRadius(10, out Collider hitCollider))
            {
                Target.Value = hitCollider.gameObject;
                return Status.Success;
            }

            return Status.Failure;
        }
    }
}