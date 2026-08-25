using _Works.JJH._02_Scripts.Agents.Players.Modules;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players
{
    public class Player : Agent
    {
        [Header("Input")]
        [field: SerializeField] public PlayerInputSO PlayerInput { get; private set; }

        public IPlayerFSM PlayerFSM { get; private set; }
        public IPlayerCamera PlayerCamera { get; private set; }

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            PlayerFSM = GetModule<IPlayerFSM>();
            Debug.Assert(PlayerFSM != null, $"{gameObject.name}에는 IPlayerFSM 모듈이 필요합니다.");
            PlayerCamera = GetModule<IPlayerCamera>();
            Debug.Assert(PlayerCamera != null, $"{gameObject.name}에는 IPlayerCamera 모듈이 필요합니다.");
        }
    }
}