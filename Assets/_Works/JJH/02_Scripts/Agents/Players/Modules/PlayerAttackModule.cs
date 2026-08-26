using _Works.JJH._02_Scripts.Agents.Players.Attacks;
using DevLib.ModuleSystem;
using System.Collections.Generic;

namespace _Works.JJH._02_Scripts.Agents.Players.Modules
{
    public class PlayerAttackModule : AbstractModule
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

            _player.PlayerInput.OnAttackKeyPressed += HandleAttackKeyPressed;
            _player.PlayerInput.OnThrowAttackKeyPressed += HandleThrowAttackKeyPressed;
        }

        private void OnDestroy()
        {
            if (_player == null || _player.PlayerInput == null)
                return;

            _player.PlayerInput.OnAttackKeyPressed -= HandleAttackKeyPressed;
            _player.PlayerInput.OnThrowAttackKeyPressed -= HandleThrowAttackKeyPressed;
        }

        private void HandleAttackKeyPressed()
        {
            if (_attacks.Count == 0)
                return;

            foreach (AbstractPlayerAttack attack in _attacks)
            {
                if (attack is AttackSkill)
                {
                    attack.Execute();
                    return;
                }
            }
        }

        private void HandleThrowAttackKeyPressed()
        {
            if (_attacks.Count == 0)
                return;

            foreach (AbstractPlayerAttack attack in _attacks)
            {
                if (attack is ThrowAttackSkill)
                {
                    attack.Execute();
                    return;
                }
            }
        }
    }
}