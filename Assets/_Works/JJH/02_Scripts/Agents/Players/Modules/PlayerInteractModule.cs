using _Works.JJH._02_Scripts.Systems.Events;
using DevLib.EventChannelSystem;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Modules
{
    public class PlayerInteractModule : AbstractModule, IPlayerInteract
    {
        [Header("Vending Machine")]
        [SerializeField] private EventChannelSO systemEvent;
        [SerializeField] private float interactDistance = 5f;
        [SerializeField] private LayerMask vendingMachineLayerMask;

        private Player _player;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _player = _owner as Player;
            Debug.Assert(_player != null, $"{gameObject.name}에는 Player가 필요합니다.");
        }

        public void ActiveVendingMachine()
        {
            if (!_player.Sensor.FindItem(Camera.main.transform, vendingMachineLayerMask,
                                                        interactDistance, out Collider _))
                return;

            systemEvent.RaiseEvent(SystemEvents.VendingMachineDropEvent);
        }
    }
}