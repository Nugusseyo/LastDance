namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>
    /// 손님 상태 하나가 끝난 방식. 상태는 "무슨 일이 있었는지"만 말하고
    /// 다음에 어디로 갈지는 모른다. 지금은 시퀀스가 선형이라 Failed만 흐름을 바꾸지만,
    /// 나중에 전이 테이블을 붙이면 나머지 값들이 분기 키가 된다.
    /// </summary>
    public enum VisitOutcome
    {
        /// <summary>할 일을 마쳤다. 다음 상태로 넘어간다.</summary>
        Done,

        /// <summary>조건이 안 맞아 아무것도 하지 않았다. 다음 상태로 넘어간다.</summary>
        Blocked,

        /// <summary>제한 시간을 넘겼다. 다음 상태로 넘어간다.</summary>
        Timeout,

        /// <summary>실패했다. 이 Phase의 남은 시퀀스를 중단한다.</summary>
        Failed
    }
}
