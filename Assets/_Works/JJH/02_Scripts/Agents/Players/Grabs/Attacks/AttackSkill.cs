using _Works.JJH._02_Scripts.Agents.Modules;
using _Works.JJH._02_Scripts.Items;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Grabs.Attacks
{
    public class AttackSkill : AbstractPlayerAttack
    {
        [Header("Detect")]
        [SerializeField] private Vector3 boxHalfExtents = new Vector3(0.5f, 0.5f, 0.75f);
        [SerializeField] private float boxDistance = 1f;
        [SerializeField] private LayerMask targetLayer;

        public override void Attack()
        {
            if (player.Grab == null || player.Grab.CurrentItem == null)
                return;

            if (player.Grab.CurrentItem.CurrentItemData is not WeaponItemSO weaponData)
                return;

            Transform origin = player.Camera.CameraTrans;
            Vector3 boxCenter = origin.position + origin.forward * boxDistance;

            Collider[] hits = Physics.OverlapBox(boxCenter, boxHalfExtents, origin.rotation, targetLayer);

            foreach (Collider hit in hits)
            {
                IHealth health = hit.GetComponentInParent<IHealth>();

                if (health == null)
                    continue;

                health.Damage(weaponData.Damage);
            }
        }
    }
}