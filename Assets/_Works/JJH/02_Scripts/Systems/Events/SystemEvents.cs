using DevLib.EventChannelSystem;

namespace _Works.JJH._02_Scripts.Systems.Events
{
    public static class SystemEvents
    {
        public static readonly VendingMachineDropEvent VendingMachineDropEvent = new VendingMachineDropEvent();
    }

    public class VendingMachineDropEvent : GameEvent { }
}