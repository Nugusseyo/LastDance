using DevLib.EventChannelSystem;

namespace _Works.KDH._01.Scripts.Vending
{
    public static class VendingEvents
    {
        public static readonly VendingSelectDropEvent VendingSelectDropEvent = new VendingSelectDropEvent();
    }

    public class VendingSelectDropEvent : GameEvent
    {
        public VendingItemSO Item { get; private set; }

        public VendingSelectDropEvent Init(VendingItemSO item)
        {
            Item = item;
            return this;
        }
    }
}
