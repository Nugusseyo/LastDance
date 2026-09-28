using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs.Attacks
{
    public abstract class AbstractPlayerAttack : MonoBehaviour
    {
        protected Player player;

        public virtual void Initialize(Player player)
        {
            this.player = player;
        }

        /// <summary>지금 공격할 수 있는지. 쿨타임이 있는 공격은 덮어쓴다.</summary>
        public virtual bool CanAttack => true;

        public abstract void Attack();
    }
}