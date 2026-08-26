using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public abstract class AbstractPlayerAttack : MonoBehaviour
    {
        protected PlayerAttackSkillModule _attackModule;

        public virtual void Initialize(PlayerAttackSkillModule attackModule)
        {
            _attackModule = attackModule;
        }

        public abstract void Attack();
    }
}