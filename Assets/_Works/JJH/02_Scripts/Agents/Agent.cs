using DevLib.ModuleSystem;

namespace JJH._02_Scripts.Agents
{
    public abstract class Agent : ModuleOwner
    {
        protected ISensor Sensor { get; private set; }

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            Sensor = GetModule<ISensor>();
        }
    }
}