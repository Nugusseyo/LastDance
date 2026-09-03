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
        public IPlayerGrab Grab { get; private set; }

        protected override void InitializeComponents()
        {

            FSM = GetModule<IPlayerFSM>();
            Debug.Assert(FSM != null, $"{gameObject.name}에는 IPlayerFSM 모듈이 필요합니다.");
            Camera = GetModule<IPlayerCamera>();
            Debug.Assert(Camera != null, $"{gameObject.name}에는 IPlayerCamera 모듈이 필요합니다.");
            AttackSkill = GetModule<IPlayerAttackSkill>();
            Debug.Assert(AttackSkill != null, $"{gameObject.name}에는 IPlayerAttackSkill 모듈이 필요합니다.");
            Grab = GetModule<IPlayerGrab>();
            Debug.Assert(Grab != null, $"{gameObject.name}에는 IPlayerGrab 모듈이 필요합니다.");

            PlayerInput.OnInteractKeyPressed += HandleFindItem;
            PlayerInput.OnAttackKeyPressed += HandleAttackKeyPressed;
            PlayerInput.OnThrowAttackKeyPressed += HandleThrowAttackKeyPressed;
            base.InitializeComponents();
        }

        private void OnDestroy()
        {
            PlayerInput.OnInteractKeyPressed -= HandleFindItem;
            PlayerInput.OnAttackKeyPressed -= HandleAttackKeyPressed;
            PlayerInput.OnThrowAttackKeyPressed -= HandleThrowAttackKeyPressed;
        }


        private void HandleFindItem()
            => Grab.PickupWeapon();
        private void HandleAttackKeyPressed()
            => AttackSkill.ChangeCurrentAttack<AttackSkill>();
        private void HandleThrowAttackKeyPressed()
            => AttackSkill.ChangeCurrentAttack<ThrowAttackSkill>();
    }
}