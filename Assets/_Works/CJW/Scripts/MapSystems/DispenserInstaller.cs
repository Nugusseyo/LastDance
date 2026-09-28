using System;
using _Works.CJW.Scripts.MapSystems.Events;
using DevLib.EventChannelSystem;
using Unity.AI.Navigation;
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
        private bool _installed;
        private int _level;
#if UNITY_EDITOR
        
        private int _debugLevel;
        
        [ContextMenu("Install")]
        private void Install()
        {
            systemChannel.RaiseEvent(MapEvents.GasStationEvent.Init(_debugLevel++));
        }
#endif
        
        private void Awake()
        {
            for(int i = 0; i < dispensers.Length; ++i)
                dispensers[i].dispenser.SetActive(false);
            
            systemChannel.AddListener<GasStationEvent>(HandleInstallDispenser);
        }

        private void HandleInstallDispenser(GasStationEvent evt)
        {
            // 이벤트로 받아온 레벨이 현재 레벨보다 작거나
            // 타겟 인덱스(_level - 1)이 배열의 길이보다 크면
            if (evt.Level < _level || _level - 1 > dispensers.Length) return;
            _level = evt.Level;
            
            var newDispenser = dispensers[_level-1];
            newDispenser.dispenser.SetActive(true);
            mapData.Register(newDispenser.dispenserPos);
            mapData.Register(newDispenser.parkingPos);
        }

        private void OnDestroy()
        {
            systemChannel.RemoveListener<GasStationEvent>(HandleInstallDispenser);
        }
    }
}