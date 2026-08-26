using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    [CreateAssetMenu(fileName = "Weapon Data", menuName = "SO/Weapon")]

    public class WeaponDataSO : ScriptableObject
    {
        public int Damage { get; private set; }
        public float AttackCooltime { get; private set; }
        public GameObject WeaponPrefab { get; private set; }
    }
}