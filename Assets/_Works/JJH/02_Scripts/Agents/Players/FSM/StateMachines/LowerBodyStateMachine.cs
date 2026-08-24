using _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates;
using DevLib.AnimatorSystem;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines
{
    public class LowerBodyStateMachine : AbstractStateMachine
    {
        private readonly Agent _agent;
        private readonly PlayerInputSO _input;

        private readonly HashDataSO _idleHash;
        private readonly HashDataSO _moveHash;
        private readonly HashDataSO _runHash;

        public LowerBodyStateMachine(Agent agent, PlayerInputSO input,
            HashDataSO idleHash, HashDataSO moveHash, HashDataSO runHash)
        {
            _agent = agent;
            _input = input;

            _idleHash = idleHash;
            _moveHash = moveHash;
            _runHash = runHash;
        }

        public void Initialize()
        {
            Idle();
        }

        public void Idle()
        {
            ChangeState(new LowerIdleState(_agent, this, _input, _idleHash));
        }

        public void Move()
        {
            ChangeState(new LowerMoveState(_agent, this, _input, _moveHash));
        }

        public void Run()
        {
            ChangeState(new LowerRunState(_agent, this, _input, _runHash));
        }
    }
}