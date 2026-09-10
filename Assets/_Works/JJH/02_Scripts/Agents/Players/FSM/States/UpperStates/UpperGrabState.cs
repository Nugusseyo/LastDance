namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates
{
    public class UpperGrabState : AbstractState
    {
        public UpperGrabState(Player player, AbstractStateMachine stateMachine)
            : base(player, stateMachine)
        {

        }

        public override void Update()
        {
            bool isGrabbed = Player.Grab.CurrentWeapon != null;
            if (isGrabbed == false)
                StateMachine.ChangeState<UpperIdleState>();
        }
    }
}