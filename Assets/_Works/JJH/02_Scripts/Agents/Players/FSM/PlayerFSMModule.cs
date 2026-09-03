using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
using _Works.JJH._02_Scripts.Agents.Players.FSM.States.LowerStates;
using _Works.JJH._02_Scripts.Agents.Players.FSM.States.UpperStates;
using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM
{
    public class PlayerFSMModule : AbstractModule, IPlayerFSM
    {
        [Header("Animation Hash")]
        [SerializeField] private HashDataSO idleHash;
        [SerializeField] private HashDataSO moveHash;
        [SerializeField] private HashDataSO runHash;
        [SerializeField] private HashDataSO grabHash;
        [SerializeField] private HashDataSO attackHash;

        public LowerBodyStateMachine LowerBody { get; private set; }
        public UpperBodyStateMachine UpperBody { get; private set; }

        private Player _player;
        private HashDataSO _currentAnimation;
        private bool _wasGrabbed;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _player = owner as Player;

            LowerBody = new LowerBodyStateMachine(_player);
            UpperBody = new UpperBodyStateMachine(_player);

            LowerBody.Initialize();
            UpperBody.Initialize();

            _player.PlayerInput.OnAttackKeyPressed += UpperBody.Attack;
            _player.PlayerInput.OnThrowAttackKeyPressed += UpperBody.Attack;
        }

        private void Update()
        {
            LowerBody.Update();

            bool isGrabbed = _player.Grab.CurrentWeapon != null;
            if (isGrabbed)
                UpperBody.Grab();
            else
                UpperBody.Idle();

            UpperBody.Update();

            UpdateAnimation();

            _wasGrabbed = isGrabbed;
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.PlayerInput.OnAttackKeyPressed -= UpperBody.Attack;
                _player.PlayerInput.OnThrowAttackKeyPressed -= UpperBody.Attack;
            }
        }

        private void UpdateAnimation()
        {
            HashDataSO nextAnimation;

            if (UpperBody.CurrentState is UpperAttackState)
                nextAnimation = attackHash;
            else if (UpperBody.CurrentState is UpperGrabState)
                nextAnimation = grabHash;
            else if (LowerBody.CurrentState is LowerRunState)
                nextAnimation = runHash;
            else if (LowerBody.CurrentState is LowerMoveState)
                nextAnimation = moveHash;
            else
                nextAnimation = idleHash;

            if (_currentAnimation == nextAnimation)
                return;

            _currentAnimation = nextAnimation;

            _player.Renderer.PlayClip(nextAnimation.HashValue, 0f, 0.1f);
        }
    }
}