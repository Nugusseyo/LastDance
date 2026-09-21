using DevLib.ModuleSystem;
using UnityEngine;

namespace DevLib.BattleSystem
{
    public struct DamageData
    {
        public float DamageAmount;
        public bool IsCritical;
        public ModuleOwner Dealer;
        public Vector2 DirectedKBForce; //방향성을 부여받은 넉백힘
    }
}