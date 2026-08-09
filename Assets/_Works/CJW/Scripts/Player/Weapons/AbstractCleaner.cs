using System;
using JJH._02_Scripts.Agents;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public abstract class AbstractCleaner : MonoBehaviour, ICleaner
    {
        [field: SerializeField] public CleanerDataSO CleanerData { get; private set; }

        public float NormalizedCooldown => Mathf.Approximately(CleanerData.Cooldown, 0f) ? 1f:
            Mathf.Clamp01((Time.time - _lastUseTime) / CleanerData.Cooldown);
        public bool IsUsing { get; private set; }
        public event Action OnCleanerUsed;
        public event Action OnCleanerEnd;
        
        protected ICleanerModule CleanerModule;
        private float _lastUseTime;
        private IRenderer _renderer;
        
        public virtual void InitializeCleaner(ICleanerModule cleanerModule)
        {
            CleanerModule = cleanerModule;
            _renderer = CleanerModule.Owner.GetModule<IRenderer>();
            cleanerModule.OnWeaponChanged += HandleCleanerEnable;
        }

        private void HandleCleanerEnable(ICleaner targetCleaner, bool enable)
        {
            if (targetCleaner == this)
            {
                if (enable)
                    EnableCleaner();
                else
                    DisableCleaner();
            }
        }

        protected virtual void DisableCleaner()
        {
            _renderer?.PlayClip(CleanerData.enterParam.HashValue, 0.1f, 0.1f);
        }
        protected virtual void EnableCleaner()
        {
            _renderer?.PlayClip(CleanerData.exitParam.HashValue, 0.1f, 0.1f);
        }

        public virtual bool CanUseCleaner()
        {
            return NormalizedCooldown >= 1f && IsUsing == false;
        }

        public virtual void UseCleaner()
        {
            _lastUseTime = Time.time;
            _renderer.PlayClip(CleanerData.useParam.HashValue, 0.1f, 0.1f);
            
            IsUsing = true;
            OnCleanerUsed?.Invoke();
        }

        public void EndCleaner()
        {
            IsUsing = false;
            OnCleanerEnd?.Invoke();
        }
    }
}