using _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates;
using DevLib.AnimatorSystem;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines
{
    public class UpperBodyStateMachine : AbstractStateMachine
    {
        private readonly Agent _agent;
        private readonly PlayerInputSO _input;

        private UpperIdleState _idleState;
        private UpperGrabState _grabState;
        private UpperAttackState _attackState;

        public UpperBodyStateMachine(Agent agent, PlayerInputSO input, HashDataSO grabHash, HashDataSO attackHash)
        {
            _agent = agent;
            _input = input;

            _idleState = new UpperIdleState(_agent, this, _input);
            _grabState = new UpperGrabState(_agent, this, _input, grabHash);
            _attackState = new UpperAttackState(_agent, this, _input, attackHash);
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