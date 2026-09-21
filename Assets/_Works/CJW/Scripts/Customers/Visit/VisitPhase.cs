namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>방문의 진행 단계. 번호를 명시하는 이유는 이 값이 프리팹의 CustomerFSMModule에 직렬화되기 때문이다. 가운데를 지우면 등록해 둔 시퀀스가 통째로 밀린다.</summary>
    public enum VisitPhase
    {
        None = 0,

        /// <summary>차량이 가게 앞으로 진입 중.</summary>
        Arriving = 1,

        /// <summary>손님이 순차적으로 하차 중.</summary>
        Unloading = 2,

        /// <summary>손님이 가게에 머무는 중. 외부에서 출발을 요청할 때까지 대기.</summary>
        Waiting = 3,

        // 4번은 비어 있다. 말풍선을 차 단위 Phase로 두려던 자리인데,
        // 지금은 손님 시퀀스의 SpeechState가 맡으므로 쓰지 않는다.
        // 번호를 당기면 아래 값들이 밀려 프리팹 데이터가 어긋난다.

        /// <summary>손님이 순차적으로 차로 돌아와 탑승 중.</summary>
        Boarding = 5,

        /// <summary>차량이 퇴장 중.</summary>
        Leaving = 6,

        /// <summary>방문 종료. 풀 반납 대기.</summary>
        Completed = 7
    }
}
