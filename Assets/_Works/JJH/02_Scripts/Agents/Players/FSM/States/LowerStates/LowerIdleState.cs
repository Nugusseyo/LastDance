using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates
{
    public class LowerIdleState : AbstractState
    {
        public LowerIdleState(Player player, AbstractStateMachine stateMachine)
            : base(player, stateMachine)
        {
            player.Mover.Stop();
        }

        public override void Update()
        {
            if (Player.PlayerInput.MoveDirection.sqrMagnitude <= 0.01f)
                return;

            LowerBodyStateMachine stateMachine = (LowerBodyStateMachine)StateMachine;

            if (Player.PlayerInput.IsSprinting)
            {
                stateMachine.Run();
                return;
            }

            stateMachine.Move();
        }
    }
}