using System;
using System.Collections.Generic;
using _Works.JYG._Scripts.Data_Container;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.Data_Container.Store;
using _Works.JYG._Scripts.Util;
using Resources.DataBase.Upgrade_Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class UpgradeBlock : MonoBehaviour
    {
        [SerializeField] private Button buyButton;
        
        [SerializeField] private TextMeshProUGUI titleTmp;  //판매 상품의 이름
        [SerializeField] private TextMeshProUGUI priceTmp;  //판매 상품의 가격
        [SerializeField] private TextMeshProUGUI countTmp;  //판매 상품의 증가값 (0.n배 증가, 50%증가 등등)
        [SerializeField] private int curLevel;  //판매 상품의 레벨
        
        [SerializeField] private UpgradeBarInitializer barInitializer;

        [SerializeField] private StoreValueContainer storeValueContainer;   //복사해서 써야함. SO라서
        private StoreValueContainer _realStoreValue;
        private StoreItem _item;
        private List<UpgradeDataWrapper> _upgradeData;

        private IntegerDataContainer _moneyManager;

        private Action onBuyButtonPressed;

        private const string Max = "MAX";

        [Header("구매, 구매 불가능, 최대업글 색상")] 
        [SerializeField] private Color AllowColor = Color.green;
        [SerializeField] private Color BlockColor = Color.red;
        [SerializeField] private Color MaxColor = Color.yellow;
        [SerializeField] private Image priceBG;
        
        public IDataContainer<float> RealStoreValue => _realStoreValue;
        public StoreItem CurItem => _item;

        public void UpgradeInit(StoreItem item, List<UpgradeDataWrapper> upgradeData, Action buyLogic, IntegerDataContainer moneyManager) //Class 받아야 함. // 받았음.
        {
            barInitializer.InitializeBar(item.maxlevel);
            //여기서 저장된 현재 레벨을 들고와 SetColor 해줘야 한다.
            UpdateUI(item);
            curLevel = item.curLevel;
            _item = item;
            
            _realStoreValue = Instantiate(storeValueContainer);
            _upgradeData = upgradeData;
            onBuyButtonPressed = buyLogic;
            buyButton.onClick.AddListener(() => onBuyButtonPressed?.Invoke());
            
            _moneyManager = moneyManager;
            _moneyManager.OnValueChanged += HandlePriceChanged;
            
            if (_moneyManager != null)
                HandlePriceChanged(_moneyManager.Value, _moneyManager.Value);
        }
        
        
        private void HandlePriceChanged(int newValue, int oldValue)
        {
            if (CurItem.curLevel == CurItem.maxlevel)
            {
                priceBG.color = MaxColor;
                buyButton.enabled = false;
                return;
            }
            
            priceBG.color = newValue >= CurItem.price ? AllowColor : BlockColor;
        }

        private void OnDestroy()
        {
            if(_moneyManager != null)
                _moneyManager.OnValueChanged -= HandlePriceChanged;
        }

        public void UpgradeRequest(StoreItem item, bool isScan) //Save & Load에서 사용되는 함수. 또는 레벨업 시 사용 되는 함수
        {
            _item = item;
            curLevel = item.curLevel;
            int startPos = isScan ? 0 : Mathf.Max(0, curLevel - 1);
            
            for (int i = startPos; i < curLevel; ++i)
            {
                barInitializer.SetColor(i, true);
            }

            SetStatusWithLevel();
            UpdateUI(item);
            
            if (_moneyManager != null)
                HandlePriceChanged(_moneyManager.Value, _moneyManager.Value);
        }
        
        private void UpdateUI(StoreItem item)
        {
            titleTmp.text = item.itemName;
            
            if (item.curLevel == item.maxlevel)
            {
                priceBG.color = MaxColor;
                buyButton.enabled = false;
                priceTmp.text = Max;
            }

            priceTmp.text = TextConvert.Get(item.price, "$");
            countTmp.text = GetStringWithUpgradeType(item.value.ValueType, item.value.Value);
        }

        private void SetStatusWithLevel()
        {
            _realStoreValue.Value = _upgradeData[curLevel].value;
        }

        public StoreItem GetUpgradeStoreItem()
        {
            if (curLevel >= _item.maxlevel)
                return default;
            
            int nextLv = curLevel + 1;
            
            UpgradeDataWrapper wrapper = _upgradeData[nextLv];
            UpgradeValue value = _item.value;
            value.Value = wrapper.value;
            
            StoreItem result = new StoreItem(
                    _item.index,
                    _item.maxlevel,
                    nextLv,
                    _item.itemName,
                    wrapper.price,
                    value);

            return result;
        }

        //ValueType과 value를 받으면 가공해서 ~%, ~회, x~ 형태로 반환해준다.
        private string GetStringWithUpgradeType(UpgradeType upgradeType, float value)
        {
            string returnText = upgradeType switch
            {
                UpgradeType.Multiply => $"x{value}",
                UpgradeType.Percent  => $"{value * 100}%",
                UpgradeType.Add      => $"{value}회",
                _                    => value.ToString()
            };
            return returnText;
        }
        
        public void DestroyRealStoreValue() => Destroy(_realStoreValue);
    }
}
