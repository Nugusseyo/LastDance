using _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons;
using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using DevLib.AnimatorSystem;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates
{
    public class UpperGrabState : AbstractState
    {
        private IPlayerGrab _playerGrab;

        private readonly HashDataSO _grabHash;

        public UpperGrabState(Agent agent, AbstractStateMachine stateMachine,
            PlayerInputSO input, HashDataSO grabHash) : base(agent, stateMachine, input)
        {
            _playerGrab = ((Player)agent).Grab;

            _grabHash = grabHash;
        }

        public override void Enter()
        {
            base.Enter();

            Agent.Renderer.PlayClip(_grabHash.HashValue, 0f, 0.1f);
        }

        public override void Update()
        {
            if (_playerGrab.CurrentWeapon == null)
                ((UpperBodyStateMachine)StateMachine).Idle();
        }
    }
}