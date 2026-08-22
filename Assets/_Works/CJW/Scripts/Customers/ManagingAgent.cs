using _Works.JJH._02_Scripts.Agents;
using DevLib.ModuleSystem;

namespace _Works.CJW.Scripts.Customers
{
    public abstract class ManagingAgent : Agent, IUpdate, IFixedUpdate
    {
        private readonly TickGroup _tickGroup = new();

        protected override void Awake()
        {
            base.Awake();
            foreach (IModule module in _moduleDict.Values)
            {
                // 틱과 무관한 모듈이 섞여 있으므로 경고 없는 쪽을 쓴다.
                _tickGroup.TryRegister(module);
            }
        }

        public void OnUpdate(float dt)
        {
            _tickGroup.Update(dt);
        }

        public void OnFixedUpdate(float dt)
        {
            _tickGroup.FixedUpdate(dt);
        }

        public void RegisterModule(IModule module)
        {
            _tickGroup.Register(module);
        }

        public void UnRegisterModule(IModule module)
        {
            _tickGroup.Unregister(module);
        }
    }
}
