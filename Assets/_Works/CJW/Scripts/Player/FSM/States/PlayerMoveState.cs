using JJH._02_Scripts.Agents;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.FSM
{
    public class PlayerMoveState : AbstractPlayerAgentState
    {
        public PlayerMoveState(Agent agent, int stateClipHash) : base(agent, stateClipHash) { }

        public override void Update()
        {
            Vector3 moveDir = _input.GetMovementDirection();
            if (moveDir.sqrMagnitude <= InputDeadZone * InputDeadZone)
            {
                _player.Fsm.ChangeState((int)PlayerLayers.Base, (int)BaseStates.Idle);
                return;
            }

            _mover.SetMovementDirection(new Vector2(moveDir.x, moveDir.z));
        }
    }
}
