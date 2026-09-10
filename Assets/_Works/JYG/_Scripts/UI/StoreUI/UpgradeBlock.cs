using System;
using Resources.DataBase.Upgrade_Data;
using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class UpgradeBlock : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleTmp;  //판매 상품의 이름
        [SerializeField] private TextMeshProUGUI priceTmp;  //판매 상품의 가격
        [SerializeField] private TextMeshProUGUI countTmp;  //판매 상품의 증가값 (0.n배 증가, 50%증가 등등)
        [SerializeField] private int curLevel;  //판매 상품의 레벨
        
        [SerializeField] private UpgradeBarInitializer barInitializer;
        //[SerializeField] private 여기에클래스입력 이름

        public void UpgradeInit(StoreItem item) //Class 받아야 함. // 받았음.
        {
            barInitializer.InitializeBar(item.level);
            //여기서 저장된 현재 레벨을 들고와 SetColor 해줘야 한다.
            titleTmp.text = item.itemName;
            priceTmp.text = item.price.ToString();
            countTmp.text = GetStringWithUpgradeType(item.value.ValueType, item.value.Value);
            curLevel = item.level;
        }

        public void UpgradeRequest(int level, bool isScan) //Save & Load에서 사용되는 함수. 또는 레벨업 시 사용 되는 함수
        {
            curLevel = level;
            int startPos = isScan ? 0 : curLevel - 1;
            
            for (int i = startPos; i < curLevel; ++i)
            {
                barInitializer.SetColor(level, true);
            }
        }
        
        //ValueType과 value를 받으면 가공해서 ~%, ~회, x~ 형태로 반환해준다.
        private string GetStringWithUpgradeType(UpgradeType upgradeType, float value)
        {
            return string.Format("{0}{1}{2}", 
                
                upgradeType switch 
                {
                UpgradeType.Multiply => 'x',
                _ => '\0' 
                }, 
                
                value,
                
                upgradeType switch
                {
                    UpgradeType.Percent => "%",
                    UpgradeType.Add => "회",
                    _ => '\0'
                });
            
            //(value)%
            //x(value)
            //(value)회
            //이걸 만들고싶었다. Enum값으로.
            //근데 너무 복잡해진거같아서, 나중에 다시 생각해봐야겠다.
        }
    }
}
