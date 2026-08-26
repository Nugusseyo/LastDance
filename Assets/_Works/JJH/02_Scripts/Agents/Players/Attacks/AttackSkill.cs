using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public class AttackSkill : AbstractPlayerAttack
    {
        [SerializeField] private float damage = 10f;

        public override void Execute()
        {
            Debug.Log($"일반 공격 실행! 데미지 : {damage}");
        }
    }
}