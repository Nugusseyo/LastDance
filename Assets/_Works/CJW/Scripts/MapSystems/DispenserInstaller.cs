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
        [SerializeField] private NavMeshSurface surfaces;
        private bool _installed;
        
        #if UNITY_EDITOR
        [ContextMenu("Install")]
        private void Install()
        {
            systemChannel.RaiseEvent(MapEvents.GasStationEvent.Init());
        }
        #endif
        
        private void Awake()
        {
            for(int i = 0; i < dispensers.Length; ++i)
                dispensers[i].dispenser.SetActive(false);
            
            systemChannel.AddListener<GasStationEvent>(HandleInstallDispenser);
        }

        private void HandleInstallDispenser(GasStationEvent obj)
        {
            if (_installed) return;
            
            _installed = true;
            for (int i = 0; i < dispensers.Length; ++i)
            {
                var oilDispenser = dispensers[i];
                oilDispenser.dispenser.SetActive(true);
                mapData.Register(oilDispenser.dispenserPos);
                mapData.Register(oilDispenser.parkingPos);
            }
        }

        private void OnDestroy()
        {
            systemChannel.RemoveListener<GasStationEvent>(HandleInstallDispenser);
        }
    }
}