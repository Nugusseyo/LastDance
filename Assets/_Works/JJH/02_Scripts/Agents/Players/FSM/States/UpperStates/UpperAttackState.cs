using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using DevLib.AnimatorSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates
{
    public class UpperAttackState : AbstractState
    {
        private readonly HashDataSO _attackHash;

        private float _attackTimer;

        public UpperAttackState(Agent agent, AbstractStateMachine stateMachine,
            PlayerInputSO input, HashDataSO attackHash) : base(agent, stateMachine, input)
        {
            _attackHash = attackHash;
        }

        public override void Enter()
        {
            _attackTimer = 0f;

            //Agent.Renderer.PlayClip(_attackHash.HashValue, 0f, 0.1f, 1);
            ((Player)Agent).AttackSkill.Attack();
        }

        public override void Update()
        {
            _attackTimer += Time.deltaTime;

            if (_attackTimer >= 0.5f)
            {
                ((UpperBodyStateMachine)StateMachine).Idle();
            }
        }
    }
}