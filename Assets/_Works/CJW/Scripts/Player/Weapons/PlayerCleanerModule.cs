using System;
using System.Collections.Generic;
using System.Linq;
using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public class PlayerCleanerModule : MonoBehaviour, IModule, ICleanerModule
    {
        public ICleaner CurrentCleaner { get; private set; }
        public bool IsUsingWeapon { get; private set; }
        public ModuleOwner Owner { get; private set; }
        public event Action OnCleanEnd;
        public event Action<ICleaner, bool> OnWeaponChanged;

        private Dictionary<int, ICleaner> _weaponDict;
        private int _currentWeaponIdx;
        
        /// <summary>
        /// 청소 도구들의 모듈 초기화
        /// </summary>
        /// <param name="owner"></param>
        public void Initialize(ModuleOwner owner)
        {
            Debug.Log("WeaponModule Initialize");
            Owner = owner;
            
            _weaponDict = GetComponentsInChildren<ICleaner>()
                .ToDictionary(weapon => weapon.CleanerData.Id);

            if (_weaponDict.Count > 0)
            {
                foreach (ICleaner weapon in _weaponDict.Values)
                {
                    weapon.InitializeCleaner(this);
                }
            }
            
            CurrentCleaner = _weaponDict[0];
        }
        /// <summary>
        /// 청소 도구를 사용하는 메서드
        /// 알아서 사용 가능한지 확인한다.
        /// </summary>
        public void UseCleaner()
        {
            if(CurrentCleaner.CanUseCleaner())
                CurrentCleaner.UseCleaner();
        }

        /// <summary>
        /// 청소 도구를 변경하는 메서드
        /// 1 ~ -1의 정수를 넣으면 클램핑을 해서 변경한다.
        /// </summary>
        /// <param name="scrollValue"></param>
        public void ChangeWeapon(int scrollValue)
        {
            _currentWeaponIdx += scrollValue;
            int nextIndex = Mathf.Clamp(_currentWeaponIdx,0, _weaponDict.Count - 1);
            if (_weaponDict.TryGetValue(nextIndex, out var weapon))
            {
                // 구독 해제
                CurrentCleaner.OnCleanerEnd -= HandleCleanerEnd;
                OnWeaponChanged?.Invoke(CurrentCleaner, false);
                
                CurrentCleaner = weapon;
                
                // 구독
                OnWeaponChanged?.Invoke(weapon, true);
                CurrentCleaner.OnCleanerEnd += HandleCleanerEnd;
                return;
            }
            
            Debug.LogWarning($"{nextIndex}에 무기가 존재하지 않습니다. |범위: {0} ~ {_weaponDict.Count - 1}|");
        }

        private void OnDestroy()
        {
            if (CurrentCleaner != null)
            {
                CurrentCleaner.OnCleanerEnd -= HandleCleanerEnd;
            }
        }

        private void HandleCleanerEnd()
        {
            OnCleanEnd?.Invoke();
        }
    }
}