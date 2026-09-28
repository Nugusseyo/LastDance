using System;
using _Works.CJW.Scripts.MapSystems.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.MapSystems
{
    public class DispenserInstaller : MonoBehaviour
    {
        [Serializable]
        public struct OilDispenser
        {
            public GameObject dispenser;
            public MapPosition dispenserPos;
            public MapPosition parkingPos;
        }
        
        [SerializeField] private EventChannelSO systemChannel;
        [SerializeField] private MapDataSo mapData;
        [SerializeField] private OilDispenser[] dispensers;
        /// <summary>지금까지 깐 주유기 수(= 적용된 레벨).</summary>
        private int _level;
#if UNITY_EDITOR
        
        private int _debugLevel = 1;
        
        [ContextMenu("Install")]
        private void Install()
        {
            systemChannel.RaiseEvent(MapEvents.GasStationEvent.Init(_debugLevel++));
        }
#endif
        
        private void Awake()
        {
            for (int i = 0; i < dispensers.Length; ++i)
            {
                if (dispensers[i].dispenser != null)
                {
                    dispensers[i].dispenser.SetActive(false);
                }
            }
            
            systemChannel.AddListener<GasStationEvent>(HandleInstallDispenser);
        }

        private void HandleInstallDispenser(GasStationEvent evt)
        {
            // 레벨 n = 주유기 n대. 배열 크기를 넘는 레벨은 있는 만큼만 깐다.
            int target = Mathf.Min(evt.Level, dispensers.Length);

            // 이미 깐 레벨 이하(같은 레벨 재전송 포함)면 중복 등록하지 않는다.
            if (target <= _level)
            {
                return;
            }

            // 레벨을 건너뛰어 와도(1→3) 사이 주유기까지 모두 깐다.
            for (int i = _level; i < target-1; ++i)
            {
                InstallAt(i);
            }

            _level = target;
        }

        private void InstallAt(int index)
        {
            OilDispenser newDispenser = dispensers[index];
            if (newDispenser.dispenser != null)
            {
                newDispenser.dispenser.SetActive(true);
            }

            mapData.Register(newDispenser.dispenserPos);
            mapData.Register(newDispenser.parkingPos);
        }

        private void OnDestroy()
        {
            systemChannel.RemoveListener<GasStationEvent>(HandleInstallDispenser);
        }
    }
}