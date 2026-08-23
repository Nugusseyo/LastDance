using JJH._02_Scripts.Agents;

namespace _Works.CJW.Scripts.Player.FSM
{
    public abstract class AbstractPlayerAgentState : AgentState
    {
        // protected readonly PlayerController _player;
        protected readonly IMover _mover;
        protected readonly InputReader _input;
        protected const float InputDeadZone = 0.1f;

        protected AbstractPlayerAgentState(Agent agent, int stateClipHash) : base(agent, stateClipHash)
        {
            // _player = (PlayerController)agent;
            // _mover = _player.GetModule<IMover>();
            // _input = _player.Input;
        }

        protected bool HasMoveInput => _input.CurrentMoveDir.sqrMagnitude > InputDeadZone * InputDeadZone;
    }
}