using UnityEngine;

namespace _Works.JYG._Scripts.Vending_Machine
{
    [CreateAssetMenu(fileName = "VendingTooltipData", menuName = "Scriptable Objects/VendingTooltipData")]
    public class VendingTooltipData : ScriptableObject
    {
        [TextArea] public string tip;
        public string itemName;
    }
}
