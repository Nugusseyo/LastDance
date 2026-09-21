using DevLib.EventChannelSystem;

namespace _Works.JYG._Scripts.Events
{
    public static class UIEvents
    {
        public static readonly GaugeEvent GaugeEvent = new GaugeEvent();
        public static readonly DurationEvent DurationEvent = new DurationEvent();
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

    public class DurationEvent : GameEvent
    {
        public float CurDuration { get; set; }
        public float MaxDuration { get; set; }

        public DurationEvent Init(float curDuration, float maxDuration)
        {
            CurDuration = curDuration;
            MaxDuration = maxDuration;
            return this;
        }
    }
}