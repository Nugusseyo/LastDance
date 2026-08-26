using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks
{
    public class ThrowAttackSkill : AbstractPlayerAttack
    {
        [SerializeField] private float damage = 20f;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform throwPoint;
        [SerializeField] private float throwForce = 10f;

        public override void Execute()
        {
            Debug.Log($"던지기 공격 실행! 데미지 : {damage}");

            if (projectilePrefab == null || throwPoint == null)
                return;

            GameObject projectile =
                Instantiate(
                    projectilePrefab,
                    throwPoint.position,
                    throwPoint.rotation
                );

            Rigidbody rigidbody = projectile.GetComponent<Rigidbody>();

            if (rigidbody != null)
            {
                rigidbody.AddForce(
                    throwPoint.forward * throwForce,
                    ForceMode.Impulse
                );
            }
        }
    }
}