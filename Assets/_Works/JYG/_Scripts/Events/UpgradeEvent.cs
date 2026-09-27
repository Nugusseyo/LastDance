using _Works.JYG._Scripts.UI.StoreUI;
using DevLib.EventChannelSystem;

namespace _Works.JYG._Scripts.Events
{
    public static class UpgradeEvent
    {
        public static readonly UpgradeItem UpgradeItem = new UpgradeItem();
    }

    public class UpgradeItem : GameEvent
    {
        public int Index { get; set; }
        public StoreItem Item { get; set; }

        public UpgradeItem Init(int index, StoreItem item)
        {
            Index = index;
            Item = item;
            return this;
        }
    }
}