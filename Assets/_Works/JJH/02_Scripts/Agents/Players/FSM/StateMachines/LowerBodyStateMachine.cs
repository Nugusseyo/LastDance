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
            SetState(_idleState, _moveState, _runState);

            ChangeState<LowerIdleState>();
        }
    }
}