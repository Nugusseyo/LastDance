using _Works.JJH._02_Scripts.Agents.Players.Grabs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Car
{
    public class SellZone : MonoBehaviour
    {
        [SerializeField] private PartSeller partSeller;

        private PlayerGrabModule playerGrab;
        private bool playerInZone;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            playerGrab = other.GetComponentInChildren<PlayerGrabModule>();
            playerInZone = true;
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            playerInZone = false;
            playerGrab = null;
        }

        private void Update()
        {
            if (!playerInZone || playerGrab == null) return;
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

            GameObject heldPart = playerGrab.CurrentGrabObject;
            if (heldPart == null) return;

            int price = partSeller.SellPart(heldPart);

            if (price > 0)
            {
                playerGrab.ClearCurrentItem();
                Debug.Log("부품을 팔았다! 가격: " + price);
            }
        }
    }
}
