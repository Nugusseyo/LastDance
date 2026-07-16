using JJH._02_Scripts.Agents;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.FSM.States
{
    // Base 레이어 : 정지 상태. 이동 입력이 들어오면 Move 로 전이한다.
    public class PlayerIdleState : AbstractPlayerAgentState
    {
        public PlayerIdleState(Agent agent, int stateClipHash) : base(agent, stateClipHash) { }

        public override void Enter(float transitionDuration, int layerIndex = 0)
        {
            base.Enter(transitionDuration, layerIndex);
            _mover.SetMovementDirection(Vector2.zero); // 이동 정지
        }

        public override void Update()
        {
            if (HasMoveInput)
            {
                _player.Fsm.ChangeState((int)PlayerLayers.Base, (int)BaseStates.Move);
            }
        }
    }
}
