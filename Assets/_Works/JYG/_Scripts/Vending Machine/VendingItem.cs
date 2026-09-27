using System;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.Events;
using _Works.JYG._Scripts.Util;
using _Works.JYG._Scripts.Vending_Machine;
using _Works.KDH._01.Scripts.Vending;
using DevLib.EventChannelSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Works.JYG.Data.Vending_Machine
{
    public class VendingItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI priceTmp;
        [SerializeField] private Image itemImage;
        [SerializeField] private Image priceBackground;
        [SerializeField] private Button buyButton;
        [SerializeField] private EventChannelSO systemEventChannel;
        [SerializeField] private EventChannelSO uiEventChannel;

        [Header("아이템 활성화, 비활성화 색상")] 
        [SerializeField] private Color greenColor;
        [SerializeField] private Color redColor;

        
        private IntegerDataContainer _moneyManager;
        private int price;
        private VendingItemSO _vendingItem;

        public void InitializeItem(VendingItemData itemData, IntegerDataContainer moneyManager)
        {
            priceTmp.text = TextConvert.Get(itemData.price, "$");
            itemImage.sprite = itemData.vendingItemSprite;
            _vendingItem = itemData.itemSO;
            
            _moneyManager = moneyManager;
            price = itemData.price;

            buyButton.onClick.AddListener(HandleBuyButtonPressed);
            
            if(_moneyManager != null)
                _moneyManager.OnValueChanged += HandleValueChanged;
        }

        public void HandleBuyButtonPressed()
        {
            if (price > _moneyManager.Value)
            {
                //ErrorMessage
                if (uiEventChannel != null)
                    uiEventChannel.RaiseEvent(UIEvents.MessageEvent.Init("물건을 살 돈이 부족합니다!"));
                return;
            }

            _moneyManager.Value -= price;
            
            if (systemEventChannel != null)
            {
                systemEventChannel.RaiseEvent(VendingEvents.VendingSelectDropEvent.Init(_vendingItem));
                Debug.Log(_vendingItem.ItemName);
            }
        }

        private void OnDestroy()
        {
            if(_moneyManager != null)
                _moneyManager.OnValueChanged -= HandleValueChanged;
            
            if(buyButton != null)
                buyButton.onClick.RemoveListener(HandleBuyButtonPressed);
        }

        private void HandleValueChanged(int newValue, int oldValue)
        {
            SetPriceBackGround(price <= newValue);
        }

        private void SetPriceBackGround(bool isGreen)
        {
            priceBackground.color = isGreen ? greenColor : redColor;
        }
    }
}