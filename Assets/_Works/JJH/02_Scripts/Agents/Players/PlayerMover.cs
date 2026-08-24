using _Works.JJH._02_Scripts.Agents.Modules;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players
{
    public class PlayerMover : AgentMover
    {
        [Header("Stamina")]
        [SerializeField] private float staminaDrainRate = 0.25f;
        [SerializeField] private float staminaRecoveryRate = 0.15f;
        [SerializeField, Range(0f, 1f)] private float stamina = 1f;

        public float Stamina => stamina;
        public bool CanRun => stamina > 0f && _sprintReleased;

        private bool _sprintReleased = true;

        public void UpdateSprintState(bool isSprinting)
        {
            if (!isSprinting)
                _sprintReleased = true;
        }

        public override void Run(Vector3 direction)
        {
            if (!CanRun)
                return;

            ConsumeStamina();

            if (stamina <= 0f)
            {
                stamina = 0f;
                _sprintReleased = false;
                return;
            }

            base.Run(direction);
        }

        public void RecoverStamina()
            => stamina = Mathf.Clamp01(stamina + staminaRecoveryRate * Time.deltaTime);

        private void ConsumeStamina()
            => stamina = Mathf.Clamp01(stamina - staminaDrainRate * Time.deltaTime);
    }
}