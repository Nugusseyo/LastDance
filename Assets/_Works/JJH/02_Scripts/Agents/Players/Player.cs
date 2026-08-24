using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players
{
    public class Player : Agent
    {
        [Header("Input")]
        [field: SerializeField] public PlayerInputSO PlayerInput { get; private set; }

        public PlayerFSMModule _fsmModule { get; private set; }

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            _fsmModule = GetModule<PlayerFSMModule>();
        }
    }
}