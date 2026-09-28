using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class MoneyDoubler : MonoBehaviour
    {
        [SerializeField] private EventChannelSO buffChannel;
        [SerializeField] private IntegerDataContainer money;

        private float endTime;
        private int bonusMoney;
        private bool isAddingBonus;

        private bool IsDoubled => Time.time < endTime;

        private void Awake()
        {
            buffChannel.AddListener<BuffEvent>(HandleBuff);
            money.OnValueChanged += HandleMoneyChanged;
        }

        private void OnDestroy()
        {
            buffChannel.RemoveListener<BuffEvent>(HandleBuff);
            money.OnValueChanged -= HandleMoneyChanged;
        }

        private void HandleBuff(BuffEvent evt)
        {
            if (evt.BuffType != BuffType.Wallet) return;

            endTime = Time.time + evt.Duration;
        }

        private void HandleMoneyChanged(int newValue, int oldValue)
        {
            if (isAddingBonus || !IsDoubled) return;

            if (newValue > oldValue)
            {
                bonusMoney += newValue - oldValue;
            }
        }

        private void LateUpdate()
        {
            if (bonusMoney <= 0) return;

            isAddingBonus = true;
            money.Value += bonusMoney;
            isAddingBonus = false;

            bonusMoney = 0;
        }
    }
}
