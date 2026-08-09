using JJH._02_Scripts.Agents;
using _Works.CJW.Scripts.Player.Weapons;

namespace _Works.CJW.Scripts.Player.FSM
{
    public class PlayerCombatState : AbstractPlayerAgentState
    {
        private readonly ICleanerModule _cleaner;
        private bool _attackRequested;

        public PlayerCombatState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _cleaner = _player.GetModule<ICleanerModule>();
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            base.Enter(transitionDuration, layerIndex);
            _attackRequested = false;
            _input.OnAttackKeyPressed += HandleAttack;
        }
        public override void Exit()
        {
            _input.OnAttackKeyPressed -= HandleAttack;
        }

        private void HandleAttack() => _cleaner.UseCleaner();
    }
}
