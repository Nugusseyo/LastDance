using DevLib.AnimatorSystem;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace JJH._02_Scripts.Agents.Enemies.BT.Actions
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "PlayClip", story: "[Enemy] play [Clip]", category: "Action/Animation", id: "7728168c1b0018ca0dc8f5155a3e4a7e")]
    public partial class PlayClipAction : Action
    {
        [SerializeReference] public BlackboardVariable<AbstractEnemy> Enemy;
        [SerializeReference] public BlackboardVariable<HashDataSO> Clip;

        protected override Status OnStart()
        {
            if (Enemy.Value == null || Enemy.Value.Renderer == null || Clip.Value == null)
                return Status.Failure;

            Enemy.Value.Renderer.PlayClip(Clip.Value.HashValue, 0.5f, 0.2f, 0);

            return Status.Success;
        }
    }
}

