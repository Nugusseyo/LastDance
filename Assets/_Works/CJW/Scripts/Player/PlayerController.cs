using _Works.CJW.Scripts.Player.FSM;
using JJH._02_Scripts.Agents;
using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public class PlayerController : Agent
    {
        [SerializeField] private InputReader input;
        [SerializeField] private StateMachineSO[] stateLayers;

        public InputReader Input => input;
        public LayeredStateMachine Fsm { get; private set; }

        protected override void AfterInitializeComponents()
        {
            base.AfterInitializeComponents();

            // 모듈 초기화가 끝난 뒤 상태 머신을 구성한다.
            Fsm = new LayeredStateMachine(stateLayers, this);

            // 각 레이어를 초기 상태로 진입시킨다.
            Fsm.ChangeState((int)PlayerLayers.Base, (int)BaseStates.Idle);
            Fsm.ChangeState((int)PlayerLayers.Upper, (int)UpperStates.Combat);
        }

        private void Update()
        {
            Fsm?.Update();
        }
    }
}
