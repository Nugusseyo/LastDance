using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using DevLib.AnimatorSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates
{
    public class LowerRunState : AbstractState
    {
        private readonly HashDataSO _runHash;

        public LowerRunState(Agent agent, AbstractStateMachine stateMachine,
            PlayerInputSO input, HashDataSO runHash) : base(agent, stateMachine, input)
        {
            _runHash = runHash;
        }

        public override void Enter()
        {
            Agent.Renderer.PlayClip(_runHash.HashValue, 0f, 0.1f);
        }

        public override void Update()
        {
            if (Input.MoveDirection.sqrMagnitude <= 0.01f)
            {
                ((LowerBodyStateMachine)StateMachine).Idle();
                return;
            }

            if (!Input.IsSprinting)
            {
                ((LowerBodyStateMachine)StateMachine).Move();
                return;
            }

            Vector3 direction = new Vector3(Input.MoveDirection.x, 0f, Input.MoveDirection.y);
            Agent.Mover.Run(direction);
        }

        public override void Exit()
        {
            Agent.Mover.Stop();
        }
    }
}