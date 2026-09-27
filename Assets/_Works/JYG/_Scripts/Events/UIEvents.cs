using DevLib.EventChannelSystem;
using Resources.DataBase.Review_Data;

namespace _Works.JYG._Scripts.Events
{
    public static class UIEvents
    {
        public static readonly GaugeEvent GaugeEvent = new GaugeEvent();
        public static readonly DurationEvent DurationEvent = new DurationEvent();
        
        public static readonly ReviewEvent ReviewEvent = new ReviewEvent();
        
        public static readonly RefuelingEvent RefuelingEvent = new RefuelingEvent();
        public static readonly ScrapEvent ScrapEvent = new ScrapEvent();
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
        public int Index { get; set; }
        public int PlusValue { get; set; }
        public ReviewType ReviewType { get; set; }

        public ReviewEvent Review(int index, ReviewType reviewType)
        {
            Index = index;
            ReviewType = reviewType;
            return this;
        }
    }
    
    #endregion
    
    #region Money Events

    public class RefuelingEvent : GameEvent
    {
        public int MoneyValue { get; set; }

        public RefuelingEvent Init(int moneyValue)
        {
            MoneyValue = moneyValue;
            return this;
        }
    }

    public class ScrapEvent : GameEvent
    {
        public int CarValue { get; set; }
        public int DisassembledWheel { get; set; }
        public int WheelPrice { get; set; }

        public ScrapEvent Init(int carValue, int disassembledWheel, int wheelPrice)
        {
            CarValue = carValue;
            DisassembledWheel = disassembledWheel;
            WheelPrice = wheelPrice;
            return this;
        }
    }
    
    #endregion
}