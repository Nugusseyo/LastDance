using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using _Works.JJH._02_Scripts.Agents.Players.Modules;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates
{
    public class LowerRunState : AbstractState
    {
        private PlayerMover _playerMover;
        private IPlayerCamera _playerCamera;

        public LowerRunState(Player player, AbstractStateMachine stateMachine)
            : base(player, stateMachine)
        {
            _playerMover = (PlayerMover)player.Mover;
            _playerCamera = player.Camera;
        }

        public override void Enter()
        {
            _playerCamera.SetCameraShake(true);
        }

        public override void Update()
        {
            _playerMover.UpdateSprintState(Player.PlayerInput.IsSprinting);

            if (Player.PlayerInput.MoveDirection.sqrMagnitude <= 0.01f)
            {
                ((LowerBodyStateMachine)StateMachine).Idle();
                return;
            }

            if (!Player.PlayerInput.IsSprinting || !_playerMover.CanRun)
            {
                ((LowerBodyStateMachine)StateMachine).Move();
                return;
            }

            Vector3 direction = new Vector3(Player.PlayerInput.MoveDirection.x, 0f,
                                                                Player.PlayerInput.MoveDirection.y);
            Player.Mover.Run(direction);
        }

        public override void Exit()
        {
            _playerCamera.SetCameraShake(false);
            Player.Mover.Stop();
        }
    }
}