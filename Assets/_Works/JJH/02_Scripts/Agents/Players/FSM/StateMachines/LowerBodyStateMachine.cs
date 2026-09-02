using _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates;
using DevLib.AnimatorSystem;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines
{
    public class LowerBodyStateMachine : AbstractStateMachine
    {
        private readonly Agent _agent;
        private readonly PlayerInputSO _input;

        private LowerIdleState _idleState;
        private LowerMoveState _moveState;
        private LowerRunState _runState;

        public LowerBodyStateMachine(Agent agent, PlayerInputSO input,
            HashDataSO idleHash, HashDataSO moveHash, HashDataSO runHash)
        {
            _agent = agent;
            _input = input;

            _idleState = new LowerIdleState(_agent, this, _input, idleHash);
            _moveState = new LowerMoveState(_agent, this, _input);
            _runState = new LowerRunState(_agent, this, _input, runHash);
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