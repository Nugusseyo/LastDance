using _Works.JJH._02_Scripts.Agents.Players.Modules;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates
{
    public class LowerIdleState : AbstractState
    {
        private PlayerMover _playerMover;

        public LowerIdleState(Player player, AbstractStateMachine stateMachine)
            : base(player, stateMachine)
        {
            _playerMover = (PlayerMover)player.Mover;
            _playerMover.Stop();
        }

        public override void Update()
        {
            if (Player.PlayerInput.MoveDirection.sqrMagnitude <= 0.01f)
                return;

            _playerMover.RecoverStamina();

            if (Player.PlayerInput.IsSprinting)
            {
                StateMachine.ChangeState<LowerRunState>();

                return;
            }

            StateMachine.ChangeState<LowerMoveState>();
        }
    }
}