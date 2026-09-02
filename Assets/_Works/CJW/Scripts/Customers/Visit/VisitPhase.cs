namespace _Works.CJW.Scripts.Customers.Visit
{
    public enum VisitPhase
    {
        None,

        /// <summary>차량이 가게 앞으로 진입 중.</summary>
        Arriving,

        /// <summary>손님이 순차적으로 하차 중.</summary>
        Unloading,

        /// <summary>손님이 가게에 머무는 중. 외부에서 출발을 요청할 때까지 대기.</summary>
        Waiting,   
        
        /// <summary>손님이 말풍선을 띄우는 중.</summary>
        Speeching,

        /// <summary>손님이 순차적으로 차로 돌아와 탑승 중.</summary>
        Boarding,

        /// <summary>차량이 퇴장 중.</summary>
        Leaving,

        /// <summary>방문 종료. 풀 반납 대기.</summary>
        Completed
    }
}
