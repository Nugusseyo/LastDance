using _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines;
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
        private bool _wasGrabbed;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _player = owner as Player;

            LowerBody = new LowerBodyStateMachine(_player, _player.PlayerInput, idleHash, moveHash, runHash);
            UpperBody = new UpperBodyStateMachine(_player, _player.PlayerInput, grabHash, attackHash);

            LowerBody.Initialize();
            UpperBody.Initialize();

            _player.PlayerInput.OnAttackKeyPressed += UpperBody.Attack;
            _player.PlayerInput.OnThrowAttackKeyPressed += UpperBody.Attack;
        }

        private void Update()
        {
            LowerBody.Update();
            UpperBody.Update();

            bool isGrabbed = _player.Grab.CurrentWeapon != null;
            if (isGrabbed && !_wasGrabbed)
                UpperBody.Grab();

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
    }
}