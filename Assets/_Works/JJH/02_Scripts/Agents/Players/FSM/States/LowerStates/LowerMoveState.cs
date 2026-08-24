using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using DevLib.AnimatorSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates
{
    public class LowerMoveState : AbstractState
    {
        private readonly HashDataSO _moveHash;

        public LowerMoveState(Agent agent, AbstractStateMachine stateMachine,
            PlayerInputSO input, HashDataSO moveHash) : base(agent, stateMachine, input)
        {
            _moveHash = moveHash;
        }

        public override void Enter()
        {
            Agent.Renderer.PlayClip(_moveHash.HashValue, 0f, 0.1f);
        }

        public override void Update()
        {
            PlayerMover playerMover = (PlayerMover)Agent.Mover;
            playerMover.UpdateSprintState(Input.IsSprinting);

            if (Input.MoveDirection.sqrMagnitude <= 0.01f)
            {
                ((LowerBodyStateMachine)StateMachine).Idle();
                return;
            }

            if (Input.IsSprinting && playerMover.CanRun)
            {
                ((LowerBodyStateMachine)StateMachine).Run();
                return;
            }

            playerMover.RecoverStamina();

            Vector3 direction = new Vector3(Input.MoveDirection.x, 0f, Input.MoveDirection.y);
            Agent.Mover.Move(direction);
        }

        public override void Exit()
        {
            Agent.Mover.Stop();
        }
    }
}