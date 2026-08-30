using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using _Works.JJH._02_Scripts.Agents.Players.Modules;
using DevLib.AnimatorSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates
{
    public class LowerRunState : AbstractState
    {
        private readonly HashDataSO _runHash;

        private PlayerMover _playerMover;
        private IPlayerCamera _playerCamera;

        public LowerRunState(Agent agent, AbstractStateMachine stateMachine,
            PlayerInputSO input, HashDataSO runHash) : base(agent, stateMachine, input)
        {
            _runHash = runHash;
        }

        public override void Enter()
        {
            _playerMover = (PlayerMover)Agent.Mover;
            _playerCamera = ((Player)Agent).Camera;

            _playerCamera.SetCameraShake(true);
            //Agent.Renderer.PlayClip(_runHash.HashValue, 0f, 0.1f);
        }

        public override void Update()
        {
            _playerMover.UpdateSprintState(PlayerInput.IsSprinting);

            if (PlayerInput.MoveDirection.sqrMagnitude <= 0.01f)
            {
                ((LowerBodyStateMachine)StateMachine).Idle();
                return;
            }

            if (!PlayerInput.IsSprinting || !_playerMover.CanRun)
            {
                ((LowerBodyStateMachine)StateMachine).Move();
                return;
            }

            Vector3 direction = new Vector3(PlayerInput.MoveDirection.x, 0f, PlayerInput.MoveDirection.y);
            Agent.Mover.Run(direction);
        }

        public override void Exit()
        {
            _playerCamera.SetCameraShake(false);
            Agent.Mover.Stop();
        }
    }
}