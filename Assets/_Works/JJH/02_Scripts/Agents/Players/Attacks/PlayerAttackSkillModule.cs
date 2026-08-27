using DevLib.ModuleSystem;
using System.Collections.Generic;
using System.Linq;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public class PlayerAttackSkillModule : AbstractModule, IPlayerAttackSkill
    {
        public IReadOnlyList<AbstractPlayerAttack> Attacks => _attacks;
        private List<AbstractPlayerAttack> _attacks = new();

        private Player _player;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _player = _owner as Player;
            _attacks.AddRange(GetComponentsInChildren<AbstractPlayerAttack>(true));

            foreach (AbstractPlayerAttack attack in _attacks)
            {
                attack.Initialize(this);
            }
        }

        public void Attack(AbstractPlayerAttack attack)
        {
            if (Attacks.Contains(attack))
                attack.Attack();
        }
    }
}