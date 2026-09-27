namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>손님 종류. 정상/비정상 구분은 <c>HumanType</c>이 들고 있고, 여기는 "무엇을 하러 왔는가"만 나눈다.
    /// 번호는 말풍선 대사 DB(Resources/DataBase/Human Data/HumanDB)의 index와 같다 — SpeechBubble이 (HumanType, 이 번호)로 대사를 찾는다.
    /// DB에 대사가 없는 손님은 DB index와 겹치지 않는 번호를 쓴다.
    /// 에셋에 정수로 직렬화되므로 번호를 바꾸면 CustomerDataSO 에셋의 값도 같이 옮겨야 한다.
    /// 실제 행동은 프리팹의 CustomerFSMModule 시퀀스가 정의한다 — 이 값은 분류·표시·대사용이다.</summary>
    public enum CustomerType
    {
        None = 0,

        // --- HumanDB에 대사가 있는 손님 (번호 = DB index) ---

        /// <summary>주유를 기다린다. 가는 곳은 랜덤. (DB 1, Good)</summary>
        Refueling = 1,

        /// <summary>차 안에서 주유를 기다린다. 하차하지 않는 손님은 프리팹의 Unloading 시퀀스를 비워서 만든다. (DB 3, Good — Bad 대사는 DB 2)</summary>
        StayInCar = 3,

        /// <summary>다른 차 앞에 가서 주먹질한다. (DB 4, Bad)</summary>
        CarBasher = 4,

        /// <summary>네고를 원한다. (DB 5, Bad)</summary>
        Negotiator = 5,

        /// <summary>주유소 안을 뺑뺑 돈다. 차에서 내리지 않는다. (DB 6, Bad)</summary>
        Circler = 6,

        /// <summary>자판기를 때린다. (DB 7, Bad — Good 대사는 DB 8)</summary>
        VendingBasher = 7,

        /// <summary>주유구가 둘 이상일 때 다른 손님과 싸운다. (DB 9, Bad — 짝의 대사는 DB 10)</summary>
        Brawler = 9,

        /// <summary>입구 도로 옆으로 가 춤을 춘다. (DB 11, Bad)</summary>
        Dancer = 11,

        // --- HumanDB에 대사가 없는 손님 ---

        /// <summary>정상이지만 이상하게 걷는다.</summary>
        OddWalker = 12,

        /// <summary>차고 옆에 차를 세우고 바퀴 교체를 요청한다. (손님은 삭제됨, 값만 남김)</summary>
        TireChange = 13,

        /// <summary>입구에 가로로 차를 대고 막는다. 차에서 내리지 않는다.</summary>
        EntranceBlocker = 21,
    }
}
