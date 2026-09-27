namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>손님 종류. 정상/비정상 구분은 <c>HumanType</c>이 들고 있고, 여기는 "무엇을 하러 왔는가"만 나눈다.
    /// 말풍선 대사 index는 여기가 아니라 프리팹의 SpeechState.lineIndices가 정한다(여러 손님이 같은 대사를 쓰고, 한 손님이 대사를 둘 가지기도 해서).
    /// 에셋에 정수로 직렬화되므로 번호를 바꾸면 CustomerDataSO 에셋의 값도 같이 옮겨야 한다.
    /// 실제 행동은 프리팹의 CustomerFSMModule 시퀀스가 정의한다 — 이 값은 분류·표시용이다.</summary>
    public enum CustomerType
    {
        None = 0,

        /// <summary>주유를 기다린다. 가는 곳은 랜덤.</summary>
        Refueling = 1,

        /// <summary>차 안에서 주유를 기다린다. 하차하지 않는 손님은 프리팹의 Unloading 시퀀스를 비워서 만든다.</summary>
        StayInCar = 3,

        /// <summary>다른 차 앞에 가서 주먹질한다.</summary>
        CarBasher = 4,

        /// <summary>네고를 원한다.</summary>
        Negotiator = 5,

        /// <summary>주유소 안을 뺑뺑 돈다. 차에서 내리지 않는다.</summary>
        Circler = 6,

        /// <summary>자판기를 때린다.</summary>
        VendingBasher = 7,

        /// <summary>주유구가 둘 이상일 때 다른 손님과 싸운다.</summary>
        Brawler = 9,

        /// <summary>입구 도로 옆으로 가 춤을 춘다.</summary>
        Dancer = 11,

        /// <summary>정상이지만 이상하게 걷는다.</summary>
        OddWalker = 12,

        /// <summary>차고 옆에 차를 세우고 바퀴 교체를 요청한다. (손님은 삭제됨, 값만 남김)</summary>
        TireChange = 13,

        /// <summary>입구에 가로로 차를 대고 막는다. 차에서 내리지 않는다.</summary>
        EntranceBlocker = 21,

        /// <summary>다른 차 앞에 가서 말을 건다. 정상 손님.</summary>
        CarTalker = 14,

        /// <summary>자판기 앞으로 간다. 때리지 않는 정상 손님.</summary>
        VendingVisitor = 15,
    }
}
