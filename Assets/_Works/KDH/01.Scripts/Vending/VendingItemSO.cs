using UnityEngine;

namespace _Works.KDH._01.Scripts.Vending
{
    [CreateAssetMenu(fileName = "Vending Item", menuName = "VendingSO/Vending Item")]
    public class VendingItemSO : ScriptableObject
    {
        [field: SerializeField] public string ItemName { get; private set; }
        [field: SerializeField] public GameObject ItemPrefab { get; private set; }
    }
}
