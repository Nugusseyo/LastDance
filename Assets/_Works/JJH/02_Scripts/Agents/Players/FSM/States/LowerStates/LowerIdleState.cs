using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using DevLib.AnimatorSystem;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates
{
    public class LowerIdleState : AbstractState
    {
        private readonly HashDataSO _idleHash;

        public LowerIdleState(Agent agent, AbstractStateMachine stateMachine,
            PlayerInputSO input, HashDataSO idleHash) : base(agent, stateMachine, input)
        {
            _idleHash = idleHash;
        }

        public override void Enter()
        {
            Agent.Mover.Stop();

            Agent.Renderer.PlayClip(_idleHash.HashValue, 0f, 0.1f);
        }

        public override void Update()
        {
            if (PlayerInput.MoveDirection.sqrMagnitude <= 0.01f)
                return;

            LowerBodyStateMachine stateMachine = (LowerBodyStateMachine)StateMachine;

            if (PlayerInput.IsSprinting)
            {
                stateMachine.Run();
                return;
            }

            stateMachine.Move();
        }
    }
}