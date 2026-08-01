using System;
using System.Collections.Generic;
using System.Linq;
using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public class PlayerWeaponModule : MonoBehaviour, IModule, IWeaponModule
    {
        public IWeapon CurrentWeapon { get; private set; }
        public bool IsUsingWeapon { get; private set; }
        public event Action<IWeapon, bool> OnWeaponChanged; 
        
        private ModuleOwner _owner;
        private Dictionary<int, IWeapon> _weaponDict;
        private int _currentWeaponIdx;
        
        public void Initialize(ModuleOwner owner)
        {
            _weaponDict = GetComponentsInChildren<IWeapon>()
                .ToDictionary(weapon => weapon.WeaponData.Id);
            _owner = owner;
            CurrentWeapon = _weaponDict[0];
        }
        public void UseWeapon()
        {
            if(CurrentWeapon.CanUseWeapon())
                CurrentWeapon.UseWeapon();
        }

        public void ChangeWeapon(int scrollValue)
        {
            _currentWeaponIdx += scrollValue;
            int nextIndex = Mathf.Clamp(_currentWeaponIdx,0, _weaponDict.Count - 1);
            if (_weaponDict.TryGetValue(nextIndex, out var weapon))
            {
                OnWeaponChanged?.Invoke(CurrentWeapon, false);
                CurrentWeapon = weapon;
                OnWeaponChanged?.Invoke(weapon, true);
                return;
            }
            
            Debug.LogWarning($"{nextIndex}에 무기가 존재하지 않습니다. |범위: {0} ~ {_weaponDict.Count - 1}|");
        }
    }
}