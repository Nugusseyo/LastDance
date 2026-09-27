using _Works.JJH._02_Scripts.Agents.Players.Grabs;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Car
{
    public class SellCounter : MonoBehaviour
    {
        [SerializeField] private PartSeller partSeller;
        [SerializeField] private EventChannelSO moneyChannel;

        private PlayerGrabModule player;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                player = other.GetComponentInChildren<PlayerGrabModule>();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                player = null;
            }
        }

        private void Update()
        {
            if (player == null) return;
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

            GameObject item = player.CurrentGrabObject;
            if (item == null) return;

            if (Sell(item))
            {
                player.ClearCurrentItem();
            }
        }

        public bool Sell(GameObject item)
        {
            ScrapCarData car = item.GetComponent<ScrapCarData>();

            if (car != null)
            {
                moneyChannel.RaiseEvent(UIEvents.ScrapEvent.Init(car.CarPrice, car.CountRemovedWheels(), car.WheelPrice));
                Destroy(item);
                return true;
            }

            return partSeller.SellPart(item) > 0;
        }
    }
}
