using System;
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

        public void InitializeItem(VendingItemData itemData)
        {
            priceTmp.text = itemData.price.ToString();
            itemImage.sprite = itemData.vendingItemSprite;
        }

        private void SetPriceBackGround(bool isGreen)
        {
            itemImage.color = isGreen ? greenColor : redColor;
        }
    }
}