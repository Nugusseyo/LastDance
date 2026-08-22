using _Works.JJH._02_Scripts.Agents.Modules;
using DevLib.ModuleSystem;

namespace _Works.JJH._02_Scripts.Agents
{
    public abstract class Agent : ModuleOwner
    {
        public IRenderer Renderer { get; private set; }
        public ISensor Sensor { get; private set; }
        public IMover Mover { get; private set; }

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            Sensor = GetModule<ISensor>();
            Renderer = GetModule<IRenderer>();
            Mover = GetModule<IMover>();
        }
    }
}