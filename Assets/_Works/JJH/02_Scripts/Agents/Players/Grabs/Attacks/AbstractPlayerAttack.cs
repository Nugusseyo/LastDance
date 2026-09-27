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

        public abstract void Attack();
    }
}