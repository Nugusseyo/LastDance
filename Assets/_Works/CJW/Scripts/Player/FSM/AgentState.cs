using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using JJH._02_Scripts.Agents;

namespace _Works.CJW.Scripts.Player.FSM
{
    public abstract class AgentState
    {
        protected readonly Agent _agent;
        protected readonly int _stateClipHash;
        protected readonly IRenderer _renderer;

        public AgentState(Agent agent, int stateClipHash)
        {
            _agent = agent;
            _stateClipHash = stateClipHash;
            _renderer = _agent.GetModule<IRenderer>();
        }

        public virtual void Enter(float transitionDuration, int layerIndex = 0)
        {
            _renderer.PlayClip(_stateClipHash, 0f, transitionDuration, layerIndex);
        }
        
        public virtual void Update(){}
        public virtual void Exit(){}
    }
}