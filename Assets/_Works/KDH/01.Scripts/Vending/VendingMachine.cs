using _Works.JJH._02_Scripts.Systems.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.KDH._01.Scripts.Vending
{

    public class VendingMachine : MonoBehaviour
    {
        [SerializeField] private VendingItemSO[] items;
        [SerializeField] private Transform vendingPoint; // Vending Tp
        [SerializeField] private EventChannelSO systemEvent;

        private void Awake()
        {
            systemEvent.AddListener<VendingMachineDropEvent>(DropVendingItem);
        }

        private void OnDestroy()
        {
            systemEvent.RemoveListener<VendingMachineDropEvent>(DropVendingItem);
        }

        private void DropVendingItem(VendingMachineDropEvent _)
        {
            if (items == null || items.Length == 0) return;

            VendingItemSO item = items[Random.Range(0, items.Length)];
            if (item == null || item.ItemPrefab == null) return;

            GameObject droppedItem = Instantiate(item.ItemPrefab, vendingPoint.position, vendingPoint.rotation);


            Collider collider = droppedItem.GetComponentInChildren<Collider>();
            if (collider == null)
            {
                droppedItem.AddComponent<BoxCollider>();
            }

            Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = droppedItem.AddComponent<Rigidbody>();
            }


            rb.constraints = RigidbodyConstraints.FreezeRotation;

            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }
}
