using DevLib.EventChannelSystem;
using Resources.DataBase.Review_Data;

namespace _Works.JYG._Scripts.Events
{
    public static class UIEvents
    {
        public static readonly GaugeEvent GaugeEvent = new GaugeEvent();
        public static readonly DurationEvent DurationEvent = new DurationEvent();
        
        public static readonly ReviewEvent ReviewEvent = new ReviewEvent();
    }

    #region GaugeEvents
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
    
    #endregion
    
    #region Review Events

    public class ReviewEvent : GameEvent
    {
        public int PlusValue { get; set; }
        public ReviewType ReviewType { get; set; }

        public ReviewEvent IncreaseValue(int plusValue, ReviewType reviewType)
        {
            PlusValue = plusValue;
            ReviewType = reviewType;
            return this;
        }
    }
    
    #endregion
}