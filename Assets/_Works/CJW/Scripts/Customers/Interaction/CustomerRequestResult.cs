namespace _Works.CJW.Scripts.Customers.Interaction
{
    /// <summary>요구 하나가 끝난 방식. 손님은 "무슨 답을 받았는지"만 알고, 그래서 어떻게 굴지는 시퀀스가 정한다.</summary>
    public enum CustomerRequestResult
    {
        /// <summary>아직 답을 못 받았다.</summary>
        Pending = 0,

        /// <summary>들어줬다.</summary>
        Accepted = 1,

        /// <summary>거절당했다.</summary>
        Rejected = 2,

        /// <summary>기다리다 지쳤다. 아무도 응답하지 않은 경우다.</summary>
        Expired = 3,

        /// <summary>손님 쪽 사정으로 요구를 거뒀다. 퇴치되거나 방문이 끊겼을 때다.</summary>
        Cancelled = 4,
    }
}
