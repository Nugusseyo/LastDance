using JJH._02_Scripts.Agents;
using _Works.CJW.Scripts.Player.Weapons;

namespace _Works.CJW.Scripts.Player.FSM
{
    public class PlayerAttackState : AbstractPlayerAgentState
    {
        private readonly ICleanerModule _cleaner;

        public PlayerAttackState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            _cleaner = _player.GetModule<ICleanerModule>();
        }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            base.Enter(transitionDuration, layerIndex); 
            _cleaner.UseCleaner();
        }

        public override void Update()
        {
            ICleaner cleaner = _cleaner.CurrentCleaner;
            if (cleaner == null || cleaner.NormalizedCooldown >= 1f)
            {
                _player.Fsm.ChangeState((int)PlayerLayers.Upper, (int)UpperStates.Combat);
            }
        }
    }
}
