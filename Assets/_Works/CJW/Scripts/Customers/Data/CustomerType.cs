namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>손님 종류. 정상/비정상 구분은 <c>HumanType</c>이 들고 있고, 여기는 "무엇을 하러 왔는가"만 나눈다.
    /// 에셋에 정수로 직렬화되므로 번호는 항상 명시하고, 새 항목은 뒤에만 추가하며, 지운 번호는 재사용하지 않는다.
    /// 실제 행동은 프리팹의 CustomerFSMModule 시퀀스가 정의한다 — 이 값은 분류·표시용이다.</summary>
    public enum CustomerType
    {
        None = 0,

        // --- 차에서 내리는 손님 ---

        /// <summary>주유를 기다린다. 가는 곳은 랜덤.</summary>
        Refueling = 1,

        /// <summary>정상이지만 이상하게 걷는다.</summary>
        OddWalker = 2,

        /// <summary>차고 옆에 차를 세우고 바퀴 교체를 요청한다.</summary>
        TireChange = 3,

        /// <summary>입구 도로 옆으로 가 춤을 춘다.</summary>
        Dancer = 4,

        /// <summary>주유구가 둘 이상일 때 다른 손님과 싸운다.</summary>
        Brawler = 5,

        /// <summary>자판기를 때린다.</summary>
        VendingBasher = 6,

        /// <summary>다른 차 앞에 가서 주먹질한다.</summary>
        CarBasher = 7,

        /// <summary>네고를 원한다.</summary>
        Negotiator = 8,

        // --- 차에서 내리지 않는 손님 ---
        // 하차하지 않는 손님은 프리팹의 Unloading 시퀀스를 비워서 만든다. 코드 분기가 아니다.

        /// <summary>차 안에서 주유를 기다린다.</summary>
        StayInCar = 20,

        /// <summary>입구에 가로로 차를 대고 막는다.</summary>
        EntranceBlocker = 21,

        /// <summary>주유소 안을 뺑뺑 돈다.</summary>
        Circler = 22,
    }
}
