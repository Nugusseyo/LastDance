using _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines
{
    public class LowerBodyStateMachine : AbstractStateMachine
    {
        private LowerIdleState _idleState;
        private LowerMoveState _moveState;
        private LowerRunState _runState;

        public LowerBodyStateMachine(Player player)
        {
            _idleState = new LowerIdleState(player, this);
            _moveState = new LowerMoveState(player, this);
            _runState = new LowerRunState(player, this);
        }

        public void Initialize()
        {
            Idle();
        }

        public void Idle()
        {
            ChangeState(_idleState);
        }

        public void Move()
        {
            ChangeState(_moveState);
        }

        public void Run()
        {
            ChangeState(_runState);
        }
    }
}