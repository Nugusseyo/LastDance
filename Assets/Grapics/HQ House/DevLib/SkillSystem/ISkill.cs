using UnityEngine;

namespace DevLib.SkillSystem
{
    public interface ISkill
    {
        SkillDataSo SkillData { get; }
        float NormalizedCooldown { get; } //0~1로 표현되는 쿨다운 값을 가지고 있어야 해
        bool IsUsing { get; }

        void InitializeSkill(ISkillModule skillModule); //스킬 모듈을 받아서 초기화하는 역할을 가져야해
        bool CanUseSkill(GameObject target = null);
        void UseSkill(GameObject target = null);
        void StopSkill(); //강제 종료
        void CleanUPSkillData();
    }
}