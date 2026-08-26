using _Works.JJH._02_Scripts.Agents.Players.Attacks;
using _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons;
using _Works.JJH._02_Scripts.Agents.Players.FSM;
using _Works.JJH._02_Scripts.Agents.Players.Modules;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players
{
    public class Player : Agent
    {
        [Header("Input")]
        [field: SerializeField] public PlayerInputSO PlayerInput { get; private set; }

        public IPlayerFSM FSM { get; private set; }
        public IPlayerCamera Camera { get; private set; }
        public IPlayerAttackSkill AttackSkill { get; private set; }
        public IPlayerWeapon Weapon { get; private set; }

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            FSM = GetModule<IPlayerFSM>();
            Debug.Assert(FSM != null, $"{gameObject.name}에는 IPlayerFSM 모듈이 필요합니다.");
            Camera = GetModule<IPlayerCamera>();
            Debug.Assert(Camera != null, $"{gameObject.name}에는 IPlayerCamera 모듈이 필요합니다.");
            AttackSkill = GetModule<IPlayerAttackSkill>();
            Debug.Assert(AttackSkill != null, $"{gameObject.name}에는 IPlayerAttackSkill 모듈이 필요합니다.");
            Weapon = GetModule<IPlayerWeapon>();
            Debug.Assert(Weapon != null, $"{gameObject.name}에는 IPlayerWeapon 모듈이 필요합니다.");
        }
    }
}