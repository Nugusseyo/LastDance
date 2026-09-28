using DevLib.EventChannelSystem;

namespace _Works.CJW.Scripts.MapSystems.Events
{
    public static class MapEvents 
    {
        public static readonly GasStationEvent GasStationEvent = new();
    }

    public class GasStationEvent : GameEvent
    {
        public int Level;
        public GasStationEvent Init(int level)
        {
            Level = level;
            return this;
        }
    }
}