using _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public abstract class AbstractPlayerAttack : MonoBehaviour
    {
        protected IPlayerAttackSkill _attackSkillModule;
        protected IPlayerWeapon _weapon;

        public virtual void Initialize(IPlayerAttackSkill attackSkillModule, IPlayerWeapon weaponModule)
        {
            _attackSkillModule = attackSkillModule;
            _weapon = weaponModule;
        }

        public abstract void Attack();
    }
}