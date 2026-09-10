using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Vending
{
    public class VendingMachineInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float interactDistance = 5f;
        [SerializeField] private LayerMask vendingMachineLayerMask;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, interactDistance, vendingMachineLayerMask))
            {
                bool hitAnything = Physics.Raycast(ray, out RaycastHit anyHit, interactDistance);
                string anyHitInfo = hitAnything ? anyHit.collider.gameObject.name + " (레이어 " + anyHit.collider.gameObject.layer + ", " + anyHit.distance.ToString("F1") + "m)" : "아무것도 없음";
                return;
            }


            VendingMachine vendingMachine = hit.collider.GetComponentInParent<VendingMachine>();
            if (vendingMachine == null)
            {
                return;
            }
            vendingMachine.DropVendingItem();
        }
    }
}
