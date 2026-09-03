using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using _Works.JJH._02_Scripts.Agents.Players.Modules;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates
{
    public class LowerMoveState : AbstractState
    {
        private PlayerMover _playerMover;

        public LowerMoveState(Agent agent, AbstractStateMachine stateMachine,
            PlayerInputSO input) : base(agent, stateMachine, input)
        {
        }

        public override void Enter()
        {
            _playerMover = (PlayerMover)Agent.Mover;
        }

        public override void Update()
        {
            _playerMover.UpdateSprintState(PlayerInput.IsSprinting);

            if (PlayerInput.MoveDirection.sqrMagnitude <= 0.01f)
            {
                ((LowerBodyStateMachine)StateMachine).Idle();
                return;
            }

            if (PlayerInput.IsSprinting && _playerMover.CanRun)
            {
                ((LowerBodyStateMachine)StateMachine).Run();
                return;
            }

            _playerMover.RecoverStamina();

            Vector3 direction = new Vector3(PlayerInput.MoveDirection.x, 0f, PlayerInput.MoveDirection.y);
            Agent.Mover.Move(direction);
        }

        public override void Exit()
        {
            Agent.Mover.Stop();
        }
    }
}