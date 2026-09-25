using DevLib.ModuleSystem;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>차 바퀴의 겉모습. 이동 수단의 속도로 바퀴를 굴리고 앞바퀴를 조향각만큼 꺾는다. 이동에는 관여하지 않는다.</summary>
    public interface ICarWheelModule : IModule
    {
        /// <summary>바퀴를 처음 모양(회전 0, 조향 0)으로 되돌린다. 풀에 돌려보낼 때 부른다.</summary>
        void ResetPose();
    }
}
