using DevLib.EventChannelSystem;
using Resources.DataBase.Review_Data;
using UnityEngine;

namespace _Works.JYG._Scripts.Events
{
    public static class UIEvents
    {
        public static readonly GaugeEvent GaugeEvent = new GaugeEvent();
        public static readonly DurationEvent DurationEvent = new DurationEvent();
        
        public static readonly ReviewEvent ReviewEvent = new ReviewEvent();
        public static readonly ReviewBlockEvent ReviewBlockEvent = new ReviewBlockEvent();
        public static readonly ReviewBuffEvent  ReviewBuffEvent = new ReviewBuffEvent();
        
        public static readonly RefuelingEvent RefuelingEvent = new RefuelingEvent();
        public static readonly ScrapEvent ScrapEvent = new ScrapEvent();
        public static readonly WalletEvent WalletEvent = new WalletEvent();
        
        public static readonly MessageEvent MessageEvent = new MessageEvent();
        
        public static readonly BuffEvent BuffEvent = new BuffEvent();

        public static readonly TooltipEvent TooltipEvent = new TooltipEvent();
        public static readonly TipMoveEvent TipMoveEvent = new TipMoveEvent();
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
        public ReviewType ReviewType { get; set; }

        public ReviewEvent Review(int index, ReviewType reviewType)
        {
            Index = index;
            ReviewType = reviewType;
            return this;
        }
    }

    public class ReviewBlockEvent : GameEvent //리뷰가 깎이지 않는 기간
    {
        public float BlockDuration { get; set; }

        public ReviewBlockEvent Init(float blockDuration)
        {
            BlockDuration = blockDuration;
            return this;
        }
    }

    public class ReviewBuffEvent : GameEvent
    {
        public float Duration { get; set; }

        public ReviewBuffEvent Init(float duration)
        {
            Duration = duration;
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

    public class WalletEvent : GameEvent
    {
        public float Duration { get; set; }

        public WalletEvent Init(float duration)
        {
            Duration = duration;
            return this;
        }
    }
    
    #endregion
    
    #region Message

    public class MessageEvent : GameEvent
    {
        public string Message { get; set; }
        public MessageEvent Init(string message)
        {
            Message = message;
            return this;
        }
    }
    
    #endregion
    
    #region BuffEvent

    public class BuffEvent : GameEvent
    {
        public BuffType BuffType { get; set; }
        public float Duration { get; set; }

        public BuffEvent Init(BuffType type, float duration)
        {
            BuffType = type;
            Duration = duration;
            return this;
        }
    }

    public enum BuffType
    {
        None,
        Energy,
        Coke,
        Cider,
        Wallet
    }
    
    #endregion
    
    #region TooltipEvent

    public class TooltipEvent : GameEvent
    {
        public string ItemName { get; set; }
        public string Content { get; set; }
        public bool Active { get; set; }

        public TooltipEvent Init(string itemName, string content, bool active)
        {
            ItemName = itemName;
            Content = content;
            Active = active;

            return this;
        }
    }

    public class TipMoveEvent : GameEvent
    {
        public Vector2 Position;

        public TipMoveEvent Init(Vector2 pos)
        {
            Position = pos;
            return this;
        }
    }
    
    #endregion
}