using _Works.JJH._02_Scripts.Agents.Players.Modules;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public abstract class AbstractPlayerAttack : MonoBehaviour
    {
        protected PlayerAttackModule _attackModule;

        public virtual void Initialize(PlayerAttackModule attackModule)
        {
            _attackModule = attackModule;
        }

        public abstract void Execute();
    }
}