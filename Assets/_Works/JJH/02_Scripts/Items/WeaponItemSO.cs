using UnityEngine;

namespace _Works.JJH._02_Scripts.Items
{
    [CreateAssetMenu(fileName = "Weapon Data", menuName = "Scriptable Objects/Item/Weapon", order = 0)]
    public class WeaponItemSO : ItemDataSO
    {
        [field: SerializeField] public int Damage { get; private set; }
        [field: SerializeField] public float AttackCooltime { get; private set; }
    }
}