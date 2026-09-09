using System;
using DevLib.ModuleSystem;
using UnityEngine;

namespace DevLib.SkillSystem
{
    public interface ISkillModule
    {
        event Action OnSkillEnd;
        ModuleOwner Owner { get; }
        
        bool CanUseSkill(int skillId, GameObject target = null);
        void UseSkill(int skillId, GameObject target = null);
        
        //스킬데이터를 받아서 해당 데이터에 기반한 기본 데미지를 산출한다.
        float GetBaseDamage(SkillDataSo skillData);
    }
}