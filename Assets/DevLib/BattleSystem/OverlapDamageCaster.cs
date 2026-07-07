using UnityEngine;

namespace DevLib.BattleSystem
{
    public class OverlapDamageCaster : AbstractDamageCaster
    {
        public enum CastType
        {
            Circle, Box
        }
        
        [SerializeField] private CastType castType;
        [SerializeField] private float radius; //circle only
        [SerializeField] private Vector2 boxSize; //box only
        
        public void SetRadius(float value) => radius = value;
        public void SetBoxSize(Vector2 value) => boxSize = value;
        
        public override bool CastDamage(float damage, Vector2 direction, float kbForce)
        {
            int cnt = castType switch
            {
                CastType.Circle => Physics2D.OverlapCircle(transform.position, radius, contactFilter, _hitResults),
                CastType.Box => Physics2D.OverlapBox(transform.position, boxSize, 0, contactFilter, _hitResults),
                _ => 0
            };

            for (int i = 0; i < cnt; i++)
            {
                if (_hitResults[i].TryGetComponent(out IDamageable damageable))
                {
                    Vector2 point = _hitResults[i].ClosestPoint(transform.position);
                    Vector2 knockbackForce = direction.normalized * kbForce;

                    DamageData damageData = new DamageData
                    {
                        DamageAmount = damage,
                        Dealer = Owner,
                        DirectedKBForce = knockbackForce
                    };
                    damageable.ApplyDamage(damageData, point, direction, -direction);
                }
            }

            return cnt > 0; //카운트가 0보다 크면 맞은거니까 true
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            if(castType == CastType.Circle)
                Gizmos.DrawWireSphere(transform.position, radius);
            else if(castType == CastType.Box)
                Gizmos.DrawWireCube(transform.position, boxSize);
        }
#endif
    }
}