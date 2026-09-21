using System;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.Util;
using _Works.JYG._Scripts.Vending_Machine;
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

        [Header("아이템 활성화, 비활성화 색상")] 
        [SerializeField] private Color greenColor;
        [SerializeField] private Color redColor;
        
        private IntegerDataContainer _moneyManager;
        private int price;

        public void InitializeItem(VendingItemData itemData, IntegerDataContainer moneyManager)
        {
            priceTmp.text = TextConvert.Get(itemData.price, "$");
            itemImage.sprite = itemData.vendingItemSprite;
            
            _moneyManager = moneyManager;
            
            if(_moneyManager != null)
                _moneyManager.OnValueChanged += HandleValueChanged;
        }

        private void OnDestroy()
        {
            if(_moneyManager != null)
                _moneyManager.OnValueChanged -= HandleValueChanged;
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