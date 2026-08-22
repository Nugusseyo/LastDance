using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.FSM.StateMachines
{
    public class PlayerFSMModule : AbstractModule
    {
        [Header("Input")]
        [SerializeField] private PlayerInputSO playerInput;

        [Header("Animation Hash")]
        [SerializeField] private HashDataSO idleHash;
        [SerializeField] private HashDataSO moveHash;
        [SerializeField] private HashDataSO runHash;
        [SerializeField] private HashDataSO attackHash;

        public LowerBodyStateMachine LowerBody { get; private set; }
        public UpperBodyStateMachine UpperBody { get; private set; }

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            Agent agent = owner as Agent;

            LowerBody = new LowerBodyStateMachine(agent, playerInput, idleHash, moveHash, runHash);
            UpperBody = new UpperBodyStateMachine(agent, playerInput, attackHash);

            LowerBody.Initialize();
            UpperBody.Initialize();

            playerInput.OnAttackKeyPressed += UpperBody.Attack;
        }

        private void Update()
        {
            LowerBody.Update();
            UpperBody.Update();
        }

        private void OnDestroy()
        {
            if (playerInput != null)
                playerInput.OnAttackKeyPressed -= UpperBody.Attack;
        }
    }
}