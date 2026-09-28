using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.MapSystems.Events;
using _Works.JJH._02_Scripts.Objects;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.JYG._Scripts.GasStation
{
    public class GasStationManager : MonoBehaviour
    {
        [SerializeField] private EventChannelSO upgradeEventChannel;
        [SerializeField] private EventChannelSO stationEvent;
        private const int UpgradeIndex = 5;
        
        private void Awake()
        {
            if(upgradeEventChannel != null)
                upgradeEventChannel.AddListener<UpgradeItem>(HandleUpgradeItem);
        }
        
        private void OnDestroy()
        {
            if(upgradeEventChannel != null)
                upgradeEventChannel.RemoveListener<UpgradeItem>(HandleUpgradeItem);
        }

        private void HandleUpgradeItem(UpgradeItem evt)
        {
            if (evt.Index != UpgradeIndex)
                return;

            ActiveGasStation(Mathf.RoundToInt(evt.Item.value.Value));
        }

        private void ActiveGasStation(int count)
        {
            if (stationEvent != null)
                stationEvent.RaiseEvent(MapEvents.GasStationEvent.Init(count));
        }
    }
}
