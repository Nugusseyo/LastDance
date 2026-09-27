using System;
using _Works.JYG._Scripts.Events;
using _Works.JYG._Scripts.UI.StoreUI;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.JYG._Scripts.Data_Container.Money
{
    public class MoneyManager : MonoBehaviour
    {
        [SerializeField] private IntegerDataContainer moneyContainer;
        [SerializeField] private IntegerDataContainer reviewContainer;
        [SerializeField] private EventChannelSO eventChannel;
        [SerializeField] private UI.StoreUI.Store store;

        private StoreItem _refuelingItem;
        private StoreItem _scrapItem;
        private const int RefuelingBonusIndex = 3; //주유 추가 보너스 인덱스는 3번이다. 
        private const int ScrapBonusIndex = 4; //폐차 보너스 인덱스는 4번이다. 
        private void Start()
        {
            if (eventChannel != null)
            {
                eventChannel.AddListener<RefuelingEvent>(HandleMoneyRefueling);
                eventChannel.AddListener<ScrapEvent>(HandleMoneyScrap);
            }

            if (store != null)
            {
                UpgradeBlock block = store.GetStoreItemWithIndex(RefuelingBonusIndex);
                block.OnValueChanged += HandleRefuelingValueChanged;
                _refuelingItem = block.GetUpgradeStoreItem();
                
                block = store.GetStoreItemWithIndex(ScrapBonusIndex);
                block.OnValueChanged += HandleScrapValueChanged;
                _scrapItem = block.GetUpgradeStoreItem();
            }
        }

        private void OnDestroy()
        {
            if (eventChannel != null)
            {
                eventChannel.RemoveListener<RefuelingEvent>(HandleMoneyRefueling);
                eventChannel.RemoveListener<ScrapEvent>(HandleMoneyScrap);
            }

            if (store != null)
            {
                store.GetStoreItemWithIndex(RefuelingBonusIndex).OnValueChanged -= HandleRefuelingValueChanged;
                store.GetStoreItemWithIndex(ScrapBonusIndex).OnValueChanged -= HandleScrapValueChanged;
            }
        }

        private void HandleMoneyRefueling(RefuelingEvent evt)
        {
            if (moneyContainer != null)
            {
                //주유 수입 = 차종 주유가 × (1 + 평판 × 0.02) × 주유 보너스 레벨
                float finalValue = evt.MoneyValue 
                                 * (1 + reviewContainer.Value * 0.02f) 
                                 * _refuelingItem.value.Value;
                moneyContainer.Value += Mathf.RoundToInt(finalValue);
            }
        }

        private void HandleMoneyScrap(ScrapEvent evt)
        {
            //빠른폐차수수료 <<-- 이거 좀 물어봐야할듯.
            if (moneyContainer != null)
            {
                //(차종 기본가 × 폐차 보너스 레벨) + 분해한 바퀴 개수 × ( 차종 바퀴가 × 폐차 보너스 레벨)
                float finalValue = (evt.CarValue * _scrapItem.value.Value)
                                   + evt.DisassembledWheel
                                   * (evt.WheelPrice * _scrapItem.value.Value);
                
                moneyContainer.Value += Mathf.RoundToInt(finalValue);
            }
        }
        
        
        private void HandleRefuelingValueChanged(StoreItem item)
            => _refuelingItem = item;
        
        private void HandleScrapValueChanged(StoreItem item) 
            => _scrapItem = item;

    }
}
