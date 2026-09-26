using _Works.JJH._02_Scripts.Agents.Modules;
using _Works.JYG._Scripts.Events;
using _Works.KDH._01.Scripts.ItemType;
using DevLib.EventChannelSystem;
using System;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Modules
{
    public class PlayerMover : AgentMover
    {
        [Header("Stamina")]
        [SerializeField] private float staminaDrainRate = 0.25f;
        [SerializeField] private float staminaRecoveryRate = 0.15f;
        [SerializeField, Range(0f, 1f)] private float stamina = 1f;

        [Header("Data")]
        [SerializeField] private EventChannelSO uiChannel;
        [SerializeField] private EventChannelSO itemEffectChannel;

        public float Stamina
        {
            get { return stamina; }
            set
            {
                stamina = Mathf.Clamp01(value);
                uiChannel.RaiseEvent(UIEvents.GaugeEvent.Init(stamina));
            }
        }
        public bool CanRun => stamina > 0f && _sprintReleased;

        private bool _sprintReleased = true;

        private float _speedMultiplier = 1f;
        private float _speedBoostTimer;

        private void Awake()
        {
            itemEffectChannel.AddListener<SpeedBoostEvent>(SpeedBoostEventHandle);
        }

        private void OnDestroy()
        {
            itemEffectChannel.RemoveListener<SpeedBoostEvent>(SpeedBoostEventHandle);
        }

        private void Update()
        {
            if (_speedBoostTimer <= 0f)
                return;

            _speedBoostTimer -= Time.deltaTime;

            if (_speedBoostTimer <= 0f)
            {
                _speedBoostTimer = 0f;

                MoveSpeed /= _speedMultiplier;
                RunSpeed /= _speedMultiplier;

                _speedMultiplier = 1f;
            }
        }

        private void SpeedBoostEventHandle(SpeedBoostEvent evt)
        {
            if (_speedBoostTimer > 0f)
            {
                MoveSpeed /= _speedMultiplier;
                RunSpeed /= _speedMultiplier;
            }

            _speedMultiplier = evt.Multiplier;
            _speedBoostTimer = evt.Duration;

            MoveSpeed *= _speedMultiplier;
            RunSpeed *= _speedMultiplier;
        }

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