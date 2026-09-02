using _Works.JJH._02_Scripts.Agents.Modules;
using _Works.Shared.Boarding;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents
{
    public abstract class Agent : ModuleOwner
    {
        public IRenderer Renderer { get; private set; }
        public ISensor Sensor { get; private set; }
        public IMover Mover { get; private set; }
        public IBoardable Boarding;

        protected override void InitializeComponents()
        {
            Renderer = GetModule<IRenderer>();
            Debug.Assert(Renderer != null, $"{gameObject.name}에는 IRenderer 모듈이 필요합니다.");
            Sensor = GetModule<ISensor>();
            Debug.Assert(Sensor != null, $"{gameObject.name}에는 ISensor 모듈이 필요합니다.");
            Mover = GetModule<IMover>();
            Debug.Assert(Mover != null, $"{gameObject.name}에는 IMover 모듈이 필요합니다.");
            Boarding = GetModule<IBoardable>();
            Debug.Assert(Boarding != null, $"{gameObject.name}에는 IBoarding 모듈이 필요합니다.");

            base.InitializeComponents();
        }
    }
}