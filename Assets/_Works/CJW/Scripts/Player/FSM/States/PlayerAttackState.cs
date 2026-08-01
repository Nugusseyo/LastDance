using JJH._02_Scripts.Agents;
using _Works.CJW.Scripts.Player.Weapons;

namespace _Works.CJW.Scripts.Player.FSM
{
    public class PlayerAttackState : AbstractPlayerAgentState
    {
        private readonly IWeaponModule _weapon;

        public PlayerAttackState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _weapon = _player.GetModule<IWeaponModule>();
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            base.Enter(transitionDuration, layerIndex); 
            _weapon.UseWeapon();
        }

        public override void Update()
        {
            IWeapon weapon = _weapon.CurrentWeapon;
            if (weapon == null || weapon.NormalizedCooldown >= 1f)
            {
                _player.Fsm.ChangeState((int)PlayerLayers.Upper, (int)UpperStates.Combat);
            }
        }
    }
}
