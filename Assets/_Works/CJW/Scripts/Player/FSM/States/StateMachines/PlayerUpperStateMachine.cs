using _Works.CJW.Scripts.Player.Weapons;
using JJH._02_Scripts.Agents;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace _Works.CJW.Scripts.Player.FSM.States.StateMachines
{
    public class PlayerUpperStateMachine : StateMachine
    {
        private IFlashLightModule _flashLight;
        private IWeaponModule _weaponModule;
        private PlayerController _player;
        private bool _isActiveLight = false;
        
        public PlayerUpperStateMachine(Agent agent, StateListSO listSO) : base(agent, listSO)
        {
            _player = agent as PlayerController;
            _weaponModule = _player.GetModule<IWeaponModule>();
            _flashLight = _player.GetModule<IFlashLightModule>();
            _player.Input.OnFlashLightKeyPressed += HandleFlashLightActive;
            _player.Input.OnScrollWheelPressed += HandleChangeEquipment;
        }

        private void HandleChangeEquipment(int scrollValue)
        {
            _weaponModule.ChangeWeapon(scrollValue);
            Debug.Log($"{scrollValue} + time: {Time.time}");
        }

        private void HandleFlashLightActive()
        {
            _flashLight.ActiveFlashLight(_isActiveLight);
            _isActiveLight = !_isActiveLight;
        }

        public override void UpdateMachine()
        {
            base.UpdateMachine();
        }

        ~PlayerUpperStateMachine()
        {
            _player.Input.OnFlashLightKeyPressed -= HandleFlashLightActive;
        }
    }
}