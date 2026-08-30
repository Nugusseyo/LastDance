using _Works.JJH._02_Scripts.Agents.Modules;
using _Works.Shared.Boarding;
using DevLib.ModuleSystem;

namespace _Works.JJH._02_Scripts.Agents
{
    public abstract class Agent : ModuleOwner
    {
        public IRenderer Renderer { get; private set; }
        public ISensor Sensor { get; private set; }
        public IMover Mover { get; private set; }

        /// <summary>탑승 능력. 이 에이전트에 탑승 모듈이 없으면 null이다.</summary>
        public IBoardable Boarding { get; private set; }

        protected override void InitializeComponents()
        {
            Sensor = GetModule<ISensor>();
            Renderer = GetModule<IRenderer>();
            Mover = GetModule<IMover>();
            Boarding = GetModule<IBoardable>();

            base.InitializeComponents();
        }
    }
}