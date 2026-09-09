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

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _player = owner as Player;

            LowerBody = new LowerBodyStateMachine(_player);
            UpperBody = new UpperBodyStateMachine(_player);

            LowerBody.Initialize();
            UpperBody.Initialize();

            _player.PlayerInput.OnAttackKeyPressed += HandleAttackKeyPressed;
            _player.PlayerInput.OnThrowAttackKeyPressed += HandleAttackKeyPressed;
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.PlayerInput.OnAttackKeyPressed -= HandleAttackKeyPressed;
                _player.PlayerInput.OnThrowAttackKeyPressed -= HandleAttackKeyPressed;
            }
        }

        private void HandleAttackKeyPressed()
        {
            UpperBody.ChangeState<UpperAttackState>();
        }

        private void Update()
        {
            LowerBody.Update();
            UpperBody.Update();

            UpdateAnimation();
        }

        private void UpdateAnimation()
        {
            HashDataSO nextAnimation;

            if (UpperBody.IsState<UpperAttackState>())
                nextAnimation = attackHash;
            else if (UpperBody.IsState<UpperGrabState>())
                nextAnimation = grabHash;
            else if (LowerBody.IsState<LowerRunState>())
                nextAnimation = runHash;
            else if (LowerBody.IsState<LowerMoveState>())
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