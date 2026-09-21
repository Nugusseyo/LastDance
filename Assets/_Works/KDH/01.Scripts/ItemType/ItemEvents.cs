using DevLib.EventChannelSystem;

namespace _Works.KDH._01.Scripts.ItemType
{
    public static class ItemEvents
    {
        public static readonly SpeedBoostEvent SpeedBoostEvent = new SpeedBoostEvent();
    }

    public class SpeedBoostEvent : GameEvent
    {
        public float Multiplier { get; private set; }
        public float Duration { get; private set; }

        public SpeedBoostEvent Init(float multiplier, float duration)
        {
            Multiplier = multiplier;
            Duration = duration;
            return this;
        }
    }
}
