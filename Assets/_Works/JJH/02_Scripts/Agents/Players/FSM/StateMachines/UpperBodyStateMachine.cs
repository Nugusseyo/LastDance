using _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates;
using DevLib.AnimatorSystem;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines
{
    public class UpperBodyStateMachine : AbstractStateMachine
    {
        private readonly Agent _agent;
        private readonly PlayerInputSO _input;

        private readonly HashDataSO _attackHash;

        public UpperBodyStateMachine(Agent agent, PlayerInputSO input, HashDataSO attackHash)
        {
            _agent = agent;
            _input = input;

            _attackHash = attackHash;
        }

        public void Initialize()
        {
            Idle();
        }

        public void Idle()
        {
            ChangeState(new UpperIdleState(_agent, this, _input));
        }

        public void Attack()
        {
            ChangeState(new UpperAttackState(_agent, this, _input, _attackHash));
        }
    }
}