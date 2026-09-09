using UnityEngine;

namespace _Works.KDH._01.Scripts.Vending
{
    // 자판기 자체를 담당. 파는 물건 목록 중 하나를 뽑아서 Vending Tp 위치에 떨어뜨린다.
    public class VendingMachine : MonoBehaviour
    {
        [SerializeField] private VendingItemSO[] items;
        [SerializeField] private Transform vendingPoint; // Vending Tp

        // 물건을 하나 뽑아서 떨어뜨린다.
        public void DropVendingItem()
        {
            if (items == null || items.Length == 0) return;

            VendingItemSO item = items[Random.Range(0, items.Length)];
            if (item == null || item.ItemPrefab == null) return;

            GameObject droppedItem = Instantiate(item.ItemPrefab, vendingPoint.position, vendingPoint.rotation);

            Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = droppedItem.AddComponent<Rigidbody>();
            }

            // 회전이 자유로우면 떨어지면서 이상하게 구르거나 눕는다.
            // 회전을 막아두면 위에서 아래로 똑바로 서서 떨어진다.
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }
}
