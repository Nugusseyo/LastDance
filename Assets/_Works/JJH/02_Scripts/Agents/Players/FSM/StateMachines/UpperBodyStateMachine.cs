using _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines
{
    public class UpperBodyStateMachine : AbstractStateMachine
    {
        private UpperIdleState _idleState;
        private UpperGrabState _grabState;
        private UpperAttackState _attackState;

        public UpperBodyStateMachine(Player player)
        {
            _idleState = new UpperIdleState(player, this);
            _grabState = new UpperGrabState(player, this);
            _attackState = new UpperAttackState(player, this);
        }

        public void Initialize()
        {
            Idle();
        }

        public void Idle()
        {
            ChangeState(_idleState);
        }

        public void Grab()
        {
            ChangeState(_grabState);
        }

        public void Attack()
        {
            ChangeState(_attackState);
        }
    }
}