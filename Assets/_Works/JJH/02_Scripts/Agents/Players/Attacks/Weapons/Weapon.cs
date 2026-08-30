using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public class Weapon : MonoBehaviour
    {
        [field: SerializeField] public WeaponDataSO CurrentWeaponData { get; private set; }
        public Rigidbody Rigidbody { get; private set; }
        public Collider Collider { get; private set; }

        private void Awake()
        {
            Rigidbody = GetComponent<Rigidbody>();
            Collider = GetComponent<Collider>();
        }
    }
}