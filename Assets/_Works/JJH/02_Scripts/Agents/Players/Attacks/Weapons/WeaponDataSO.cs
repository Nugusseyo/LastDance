using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    [CreateAssetMenu(fileName = "Weapon Data", menuName = "SO/Weapon")]

    public class WeaponDataSO : ScriptableObject
    {
        [field: SerializeField] public int Damage { get; private set; }
        [field: SerializeField] public float AttackCooltime { get; private set; }
    }
}