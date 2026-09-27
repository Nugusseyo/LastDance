using _Works.JJH._02_Scripts.Agents.Modules;
using System.Collections.Generic;
using _Works.JJH._02_Scripts.Items;
using _Works.Shared.Combat;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs.Attacks
{
    public class AttackSkill : AbstractPlayerAttack
    {
        [Header("Detect")]
        [SerializeField] private Vector3 boxHalfExtents = new Vector3(0.5f, 0.5f, 0.75f);
        [SerializeField] private float boxDistance = 1f;
        [SerializeField] private LayerMask targetLayer;

        [Header("Hit")]
        [Tooltip("맞은 대상(손님)을 밀어내는 세기(m/s). 죽을 때 이 세기로 날아간다.")]
        [SerializeField] private float hitForce = 4f;

        private readonly HashSet<IHittable> _hitTargets = new();

        private float _cooldownTimer;

        public bool IsOnCooldown => _cooldownTimer > 0f;

        private void Update()
        {
            if (_cooldownTimer > 0f)
                _cooldownTimer -= Time.deltaTime;
        }

        public override void Attack()
        {
            if (IsOnCooldown)
                return;

            if (player.Grab == null || player.Grab.CurrentItem == null)
                return;

            if (player.Grab.CurrentItem.CurrentItemData is not WeaponItemSO weaponData)
                return;

            _cooldownTimer = weaponData.AttackCooltime;

            Transform origin = player.Camera.CameraTrans;
            Vector3 boxCenter = origin.position + origin.forward * boxDistance;

            Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, origin.rotation, targetLayer);

            // 한 대상이 콜라이더를 여러 개 가져도 한 번만 때린다.
            _hitTargets.Clear();

            foreach (Collider hit in hits)
            {
                // 손님처럼 맞은 방향으로 밀려나는 대상. 때린 방향과 때린 사람을 같이 넘긴다.
                IHittable hittable = hit.GetComponentInParent<IHittable>();

                if (hittable != null)
                {
                    if (_hitTargets.Add(hittable))
                        hittable.TakeHit(new HitInfo(weaponData.Damage, origin.forward, hitForce, player.gameObject));

                    continue;
                }

                IHealth health = hit.GetComponentInParent<IHealth>();

                if (health == null)
                    continue;

                health.Damage(weaponData.Damage);
            }
        }
    }
}