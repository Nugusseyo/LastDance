using UnityEngine;

namespace _Works.CJW.Scripts.Player.Weapons
{
    [CreateAssetMenu(fileName = "new weapon data", menuName = "JW/Player/WeaponData", order = 0)]
    public class WeaponDataSO : ScriptableObject
    {
        public int Id;
        public int Cooldown;
    }
}