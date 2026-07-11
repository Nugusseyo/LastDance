using DevLib.ModuleSystem;
using UnityEngine;

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
            Debug.Assert(Sensor != null, $"{gameObject.name}에는 ISensor모듈이 필요합니다.");
            Renderer = GetModule<IRenderer>();
            Debug.Assert(Renderer != null, $"{gameObject.name}에는 IRenderer모듈이 필요합니다.");
        }
    }
}