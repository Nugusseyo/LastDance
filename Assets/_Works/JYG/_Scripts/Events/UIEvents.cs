using DevLib.EventChannelSystem;

namespace _Works.JYG._Scripts.Events
{
    public static class UIEvents
    {
        public static readonly GaugeEvent GaugeEvent = new GaugeEvent();
    }

    public class GaugeEvent : GameEvent
    {
        public float Value { get; set; }
        public GaugeEvent Init(float value)
        {
            Value = value;
            return this;
        }
    }
}