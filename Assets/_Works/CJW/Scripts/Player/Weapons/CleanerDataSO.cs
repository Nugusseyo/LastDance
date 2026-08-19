using DevLib.AnimatorSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.Weapons
{
    [CreateAssetMenu(fileName = "new weapon data", menuName = "JW/Player/WeaponData", order = 0)]
    public class CleanerDataSO : ScriptableObject
    {
        [field: SerializeField] public HashDataSO enterParam { get; private set; }
        [field: SerializeField] public HashDataSO exitParam { get; private set; }
        [field: SerializeField] public HashDataSO useParam { get; private set; }
        
        public int Id;
        public int Cooldown;
    }
}