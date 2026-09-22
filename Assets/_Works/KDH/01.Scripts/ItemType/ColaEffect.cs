using _Works.JYG._Scripts.Data_Container.Money;
using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class ColaEffect : MonoBehaviour, IItemEffect
    {
        [SerializeField] private IntegerDataContainer rating;

        public void Apply()
        {
            rating.Value *= 2;
        }
    }
}
