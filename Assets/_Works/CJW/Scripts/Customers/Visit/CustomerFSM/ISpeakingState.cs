namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>대사(HumanDB index)를 말하는 행동. 리뷰 글은 손님 대사와 같은 번호를 쓰므로(ReviewDB와 번호를 맞춰 둠),
    /// 손님이 아직 말하기 전에 평판이 바뀌어도 이 번호로 그 손님다운 리뷰 글을 고른다.</summary>
    public interface ISpeakingState
    {
        /// <summary>이 행동이 말할 대사 index 후보. 말하지 않으면 비어 있거나 null.</summary>
        int[] SpokenLines { get; }
    }
}
