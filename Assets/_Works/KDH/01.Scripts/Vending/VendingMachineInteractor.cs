using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Vending
{
    // 플레이어가 자판기를 바라본 상태에서 E키를 누르면 자판기가 물건을 뽑게 시킨다.
    public class VendingMachineInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactDistance = 3f;
        [SerializeField] private LayerMask vendingMachineLayerMask;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, vendingMachineLayerMask)) return;

            VendingMachine vendingMachine = hit.collider.GetComponent<VendingMachine>();
            if (vendingMachine == null) return;

            vendingMachine.DropVendingItem();
        }
    }
}
