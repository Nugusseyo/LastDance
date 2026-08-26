using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public class ThrowAttackSkill : AbstractPlayerAttack
    {
        [SerializeField] private float damage = 20f;
        [SerializeField] private float throwForce = 10f;

        public override void Attack()
        {
            Debug.Log($"던지기 공격 실행! 데미지 : {damage}");
        }
    }
}