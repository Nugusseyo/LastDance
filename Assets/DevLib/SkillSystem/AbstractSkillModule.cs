using System;
using System.Collections.Generic;
using System.Linq;
using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using UnityEngine;

namespace DevLib.SkillSystem
{
    public abstract class AbstractSkillModule : Module, ISkillModule
    {
        public event Action OnSkillEnd;
        public ModuleOwner Owner => _owner;
        
        protected Dictionary<int, AbstractSkill> _skillDict;
        
        public AbstractSkill CurrentSkill { get; protected set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);
            _skillDict = GetComponentsInChildren<AbstractSkill>().ToDictionary(skill => skill.SkillData.skillIdHash.HashValue);
            foreach (AbstractSkill skill in _skillDict.Values)
            {
                skill.InitializeSkill(this);
            }
        }

        public virtual bool CanUseSkill(int skillId, GameObject target = null)
        {
            if (_skillDict.TryGetValue(skillId, out AbstractSkill skill))
            {
                return skill.CanUseSkill(target);
            }

            return false;
        }

        public virtual void UseSkill(int skillId, GameObject target = null)
        {
            if(_skillDict.TryGetValue(skillId, out AbstractSkill skill))
            {
                if (CurrentSkill is { IsUsing: true })
                {
                    CurrentSkill.StopSkill(); //강제 종료
                    CurrentSkill.OnSkillEnd -= HandleSkillEnd;
                }
                
                CurrentSkill = skill;
                skill.OnSkillEnd += HandleSkillEnd;
                skill.UseSkill(target);
            }
        }

        private void HandleSkillEnd(AbstractSkill endSkill)
        {
            endSkill.OnSkillEnd -= HandleSkillEnd;
            if(endSkill == CurrentSkill)
                CurrentSkill = null;
            OnSkillEnd?.Invoke();
        }

        public float GetNormalizedCooldown(int skillId)
        {
            if (_skillDict != null && _skillDict.TryGetValue(skillId, out AbstractSkill skill))
                return skill.NormalizedCooldown;
            return 0f;
        }

        public abstract float GetBaseDamage(SkillDataSo skillData);
    }
}