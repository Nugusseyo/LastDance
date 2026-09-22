using System;
using _Works.JYG._Scripts.Data_Container.Money;
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
        [SerializeField] private EventChannelSO eventChannelSO;

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

            buyButton.onClick.AddListener(HandleBuyButtonPressed);
            
            if(_moneyManager != null)
                _moneyManager.OnValueChanged += HandleValueChanged;
        }

        public void HandleBuyButtonPressed()
        {
            if (price > _moneyManager.Value)
            {
                //ErrorMessage
                return;
            }

            _moneyManager.Value -= price;
            
            if (eventChannelSO != null)
            {
                eventChannelSO.RaiseEvent(VendingEvents.VendingSelectDropEvent.Init(_vendingItem));
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