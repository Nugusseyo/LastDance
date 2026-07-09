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

        protected override Status OnStart()
        {
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            return Status.Success;
        }

        protected override void OnEnd()
        {
        }
    }
}