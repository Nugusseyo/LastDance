using UnityEngine;

namespace DevLib.BattleSystem
{
    public interface IDamageable
    {
        void ApplyDamage(DamageData damageData, Vector2 hitPoint, Vector2 hitDirection, Vector2 hitNormal);
    }
}