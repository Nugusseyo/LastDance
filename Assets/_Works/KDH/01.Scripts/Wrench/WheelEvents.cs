using DevLib.EventChannelSystem;

namespace _Works.KDH._01.Scripts.Wrench
{
    public static class WheelEvents
    {
        public static readonly WheelDetachProgressEvent WheelDetachProgressEvent = new WheelDetachProgressEvent();
    }

    public class WheelDetachProgressEvent : GameEvent
    {
        public float Current { get; private set; }
        public float Max { get; private set; }
        public bool IsActive { get; private set; }

        public WheelDetachProgressEvent Init(float current, float max, bool isActive)
        {
            Current = current;
            Max = max;
            IsActive = isActive;
            return this;
        }
    }
}
