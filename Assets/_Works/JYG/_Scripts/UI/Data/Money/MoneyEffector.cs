using _Works.JYG._Scripts.Data_Container.Money;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.Data.Money
{
    public class MoneyEffector : MonoBehaviour
    {
        [SerializeField] private IntegerDataContainer moneyManager;
        [SerializeField] private PoolManagerSO poolManager;

        private void Awake()
        {
            if (moneyManager != null)
                moneyManager.OnValueChanged += HandleValueChanged;
        }

        private void OnDestroy()
        {
            if(moneyManager != null)
                moneyManager.OnValueChanged -= HandleValueChanged;
        }

        private void HandleValueChanged(int newValue, int oldValue)
        {
            
        }
    }
}
