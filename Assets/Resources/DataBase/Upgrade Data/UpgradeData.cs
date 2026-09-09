using System;

namespace Resources.DataBase.Upgrade_Data
{
    [Serializable]
    public class UpgradeData
    {
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

        public int lv1value; //업그레이드 시 증가하는 밸류
        public int lv2value;
        public int lv3value;
        public int lv4value;
        public int lv5value;
        public int lv6value;
        public int lv7value;
        public int lv8value;
        public int lv9value;
    }
}
