using System;
using System.Collections.Generic;
using _Works.JJH._02_Scripts.Objects;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.JYG._Scripts.GasStation
{
    public class GasStationManager : MonoBehaviour
    {
        public List<GasStationController> gasStations = new List<GasStationController>();
        [SerializeField] private EventChannelSO upgradeEventChannel;
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
            int cnt = Mathf.Clamp(count, 1, gasStations.Count - 1);
            for (int i = 0; i < count; ++i)
            {
                GasStationController controller = gasStations[i];
                controller.gameObject.SetActive(true);
            }
        }
    }
}
