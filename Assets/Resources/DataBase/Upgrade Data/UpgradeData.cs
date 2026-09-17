using System;
using System.Collections.Generic;
using UnityEngine;

namespace Resources.DataBase.Upgrade_Data
{
    [Serializable]
    public class UpgradeData
    {
        public int index;
        public string value; //UI에서 나올 아이템의 이름
        public int maxlv; //최대 레벨 (Upgrade Initializer에서 참조)
        
        //엑셀에서 일단 이렇게 다 받아오고있는데, 어떻게 최적화 해야할지 모르겠음.;;
        public int lv1price; //레벨 n 가격
        public int lv2price;
        public int lv3price;
        public int lv4price;
        public int lv5price;
        public int lv6price;
        public int lv7price;
        public int lv8price;
        public int lv9price;

        public float lv1value; //업그레이드 시 증가하는 밸류
        public float lv2value;
        public float lv3value;
        public float lv4value;
        public float lv5value;
        public float lv6value;
        public float lv7value;
        public float lv8value;
        public float lv9value;

        public List<UpgradeDataWrapper> GetNormalizedValue()
        {
            // Percent : 0.6 이런형태로 건넴. 1이 100%임.
            // Multiply : 0.1배 = 0.1 형태로 전달함.
            // Add : 저장된 값 그대로 반환
            List<UpgradeDataWrapper> listValue = new()
            {
                new UpgradeDataWrapper(lv1price, lv1value),
                new UpgradeDataWrapper(lv2price, lv2value),
                new UpgradeDataWrapper(lv3price, lv3value),
                new UpgradeDataWrapper(lv4price, lv4value),
                new UpgradeDataWrapper(lv5price, lv5value),
                new UpgradeDataWrapper(lv6price, lv6value),
                new UpgradeDataWrapper(lv7price, lv7value),
                new UpgradeDataWrapper(lv8price, lv8value),
                new UpgradeDataWrapper(lv9price, lv9value),
            };

            float multiplier = 1;
            switch (upgradetype) //저장된 값에 특수 로직 실행. 아직까진 Percent만 있다.
            {
                case UpgradeType.None:
                    Debug.LogError("UpgradeDB에서, upgradetype이 제대로 정의가 되지 않은 데이터가 존재합니다. GetNormalizedValue 거절됨, index : " + index);
                    return null;
                
                case UpgradeType.Percent:
                    multiplier = 0.01f;
                    break;
                
                default:
                    break;
            }

            for(int i = listValue.Count - 1; i >= 0; i--)
            {
                UpgradeDataWrapper wrapper = listValue[i];
                if (wrapper.price == 0 && wrapper.value == 0)
                {
                    listValue.RemoveAt(i);
                    continue;
                }
                wrapper.value *= multiplier;
            }
            
            return listValue;
        }

        public UpgradeType upgradetype; //%로 받을건지, 곱하기로 받을건지 등
    }

    public class UpgradeDataWrapper
    {
        public int price;       //업그레이드 가격
        public float value;     //증가값

        public UpgradeDataWrapper(int price, float value)
        {
            this.price = price;
            this.value = value;
        }
    }

    public enum UpgradeType
    {
        None,
        Percent,
        Multiply,
        Add
    }
}
