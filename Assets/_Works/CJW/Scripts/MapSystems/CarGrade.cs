namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>차의 등급. 평판에 따라 해금되고, 해금된 등급끼리 가중치로 등장 확률을 나눈다.</summary>
    public enum CarGrade
    {
        /// <summary>등급 추첨에 끼지 않는다. 등급 있는 차가 하나도 없을 때만 예전 방식(spawnWeight)으로 뽑힌다.</summary>
        None = 0,
        Low = 1,
        Mid = 2,
        High = 3,
        Super = 4,
    }
}
