using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Modules
{
    public class AgentHealth : AbstractModule
    {
        public float Health
        {
            get => _health;
            set
            {
                if (_health <= 0)
                    _health = 0;
                else
                    _health = Mathf.Min(value, _maxHealth);
            }
        }
        private float _health;

        private float _maxHealth;

        public void InitHealth(float maxHealth)
        {
            _maxHealth = maxHealth;
            Health = _maxHealth;
        }

        public void Damage(float damage)
            => Health -= damage;
    }
}