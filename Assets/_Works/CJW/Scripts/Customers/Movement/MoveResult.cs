namespace _Works.CJW.Scripts.Customers.Movement
{
    /// <summary>이동 한 번이 끝난 방식. 이동은 "무슨 일이 있었는지"만 말하고, 그래서 어떻게 할지는 부르는 쪽이 정한다.</summary>
    public enum MoveResult
    {
        /// <summary>목적지에 닿았다.</summary>
        Done,

        /// <summary>길이 없거나 Agent가 살아 있지 않아 움직이지 못했다.</summary>
        Blocked,

        /// <summary>제한 시간 안에 닿지 못했다.</summary>
        Timeout
    }
}
