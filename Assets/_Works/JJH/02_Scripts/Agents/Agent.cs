using DevLib.ModuleSystem;

namespace JJH._02_Scripts.Agents
{
    public abstract class Agent : ModuleOwner
    {
        public IRenderer Renderer { get; private set; }
        public ISensor Sensor { get; private set; }

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            Sensor = GetModule<ISensor>();
            Renderer = GetModule<IRenderer>();
        }
    }
}