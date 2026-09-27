using UnityEngine;

namespace _Works.JJH._02_Scripts.Items
{
    [CreateAssetMenu(fileName = "Use Item Data", menuName = "Scriptable Objects/Item/Use", order = 1)]
    public class UseItemSO : ItemDataSO
    {
        [field: SerializeField] public float Multiplier { get; private set; }
        [field: SerializeField] public float Duration { get; private set; }
    }
}