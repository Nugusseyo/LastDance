using System;
using DevLib.EventChannelSystem;
using DevLib.SoundSystem;
using UnityEngine;

namespace DevLib.SkillSystem
{
    public abstract class AbstractSkill : MonoBehaviour, ISkill
    {
        public event Action<AbstractSkill> OnSkillEnd;
        [field: SerializeField] public SkillDataSo SkillData { get; private set; }

        [SerializeField] protected EventChannelSO soundChannel;
        [SerializeField] protected SoundClipSo skillSound;
        
        public float NormalizedCooldown
        {
            get
            {
                if (SkillData == null || SkillData.cooldown <= 0f) return 0f;
                return Mathf.Clamp01(1f - (Time.time - _lastUsedTime) / SkillData.cooldown);
            }
        }
        public bool IsUsing { get; private set; }

        protected AbstractSkillModule _skillModule;
        protected float _lastUsedTime = float.NegativeInfinity;
        
        public virtual void InitializeSkill(ISkillModule skillModule)
        {
            _skillModule = skillModule as AbstractSkillModule;
        }

        public abstract bool CanUseSkill(GameObject target = null);

        public virtual void UseSkill(GameObject target = null)
        {
            IsUsing = true;
        }

        public void StopSkill()
        {
            CleanUPSkillData();
        }

        public virtual void CleanUPSkillData()
        {
            _lastUsedTime = Time.time;
            IsUsing = false;
            OnSkillEnd?.Invoke(this);
        }
    }
}