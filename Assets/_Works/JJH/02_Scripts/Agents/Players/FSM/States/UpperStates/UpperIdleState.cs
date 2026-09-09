namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates
{
    public class UpperIdleState : AbstractState
    {
        public UpperIdleState(Player agent, AbstractStateMachine stateMachine)
            : base(agent, stateMachine)
        {
        }

        public override void Update()
        {
            bool isGrabbed = Player.Grab.CurrentWeapon != null
                                        && !StateMachine.IsState<UpperAttackState>();
            if (isGrabbed)
                StateMachine.ChangeState<UpperGrabState>();
            else
                StateMachine.ChangeState<UpperIdleState>();
        }
    }
}