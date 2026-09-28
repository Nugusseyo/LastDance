using DevLib.EventChannelSystem;

namespace _Works.CJW.Scripts.MapSystems.Events
{
    public static class MapEvents 
    {
        public static readonly GasStationEvent GasStationEvent = new();
    }

    public class GasStationEvent : GameEvent
    {
        public GasStationEvent Init()
        {
            return this;
        }
    }
}