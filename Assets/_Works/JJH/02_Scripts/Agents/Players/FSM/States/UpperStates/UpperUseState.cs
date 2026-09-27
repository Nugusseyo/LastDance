using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates
{
    public class UpperUseState : AbstractState
    {
        private float _useTimer;

        public UpperUseState(Player player, AbstractStateMachine stateMachine)
            : base(player, stateMachine)
        {
        }

        public override void Enter()
        {
            _useTimer = 0f;

            Player.Grab.UseItem();
        }

        public override void Update()
        {
            _useTimer += Time.deltaTime;

            if (_useTimer >= 1f)
            {
                StateMachine.ChangeState<UpperIdleState>();
            }
        }
    }
}