using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates
{
    public class UpperAttackState : AbstractState
    {
        private float _attackTimer;

        public UpperAttackState(Player player, AbstractStateMachine stateMachine)
            : base(player, stateMachine)
        {
        }

        public override void Enter()
        {
            _attackTimer = 0f;

            Player.AttackSkill.Attack();
        }

        public override void Update()
        {
            _attackTimer += Time.deltaTime;

            if (_attackTimer >= 0.5f)
            {
                ((UpperBodyStateMachine)StateMachine).Grab();
            }
        }
    }
}