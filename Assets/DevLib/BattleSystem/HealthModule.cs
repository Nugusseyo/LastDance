using System;
using DevLib.ModuleSystem;
using UnityEngine;

namespace DevLib.BattleSystem
{
    public class HealthModule : Module
    {
        [field: SerializeField] public float MaxHealth { get; private set;} = 50f;
        public delegate void HealthChange(float before, float current, float max);
        public event HealthChange OnHealthChange;
        public event Action OnDead;
        
        [SerializeField] private float _currentHealth;

        public float CurrentHealth
        {
            get => _currentHealth;
            set
            {
                float before = _currentHealth;
                _currentHealth = Mathf.Clamp(value, 0f, MaxHealth);
                if (!Mathf.Approximately(before, _currentHealth))
                {
                    OnHealthChange?.Invoke(before, _currentHealth, MaxHealth);
                }
            }
        }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            CurrentHealth = MaxHealth;
        }

        public void TakeDamage(float damage)
        {
            CurrentHealth -= damage;
            
            if (CurrentHealth <= 0)
            {
                OnDead?.Invoke();
            }
        }
    }
}