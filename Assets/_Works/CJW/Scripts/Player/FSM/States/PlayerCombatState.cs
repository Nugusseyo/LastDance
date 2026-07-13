using JJH._02_Scripts.Agents;
using _Works.CJW.Scripts.Player.Weapons;

namespace _Works.CJW.Scripts.Player.FSM
{
    public class PlayerCombatState : AbstractPlayerAgentState
    {
        private readonly IWeaponModule _weapon;
        private bool _attackRequested;

        public PlayerCombatState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _weapon = _player.GetModule<IWeaponModule>();
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

        private void HandleAttack() => _weapon.UseWeapon();
    }
}
