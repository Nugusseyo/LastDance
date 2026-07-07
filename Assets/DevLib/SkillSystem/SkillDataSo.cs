using System;
using DevLib.AnimatorSystem;
using UnityEngine;

namespace DevLib.SkillSystem
{
    public enum SkillType
    {
        Physical, Magic, NonDamage
    }

    public enum DirectionType
    {
        Body, Pointer
    }
    
    [CreateAssetMenu(fileName = "SkillData", menuName = "Lib/Skill/SkillData", order = 0)]
    public class SkillDataSo : ScriptableObject
    {
        public Sprite icon;
        public SkillType skillType;
        public DirectionType directionType;
        public float maxRange;
        public HashDataSO skillIdHash;
        public HashDataSO skillAnimationHash;
        public float damageMultiplier = 1f;
        public float kbForce;
        public float cooldown;
        
    }
}