using UnityEngine;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Vending
{
    // 플레이어가 자판기를 바라본 상태에서 E키를 누르면 자판기가 물건을 뽑게 시킨다.
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
                // 레이어 상관없이 뭐라도 맞는지 같이 찍어서, 조준이 빗나간 건지 거리가 먼 건지 구분한다
                bool hitAnything = Physics.Raycast(ray, out RaycastHit anyHit, interactDistance);
                string anyHitInfo = hitAnything ? anyHit.collider.gameObject.name + " (레이어 " + anyHit.collider.gameObject.layer + ", " + anyHit.distance.ToString("F1") + "m)" : "아무것도 없음";
                Debug.Log("[Vending] E 눌렀는데 자판기 못 맞춤. 카메라 위치=" + playerCamera.transform.position + " 이번 레이가 실제로 맞은 것=" + anyHitInfo);
                return;
            }

            // 레이가 맞은 건 자판기 자식 오브젝트(메쉬)라서 부모 쪽까지 같이 찾아야 한다
            VendingMachine vendingMachine = hit.collider.GetComponentInParent<VendingMachine>();
            if (vendingMachine == null)
            {
                Debug.Log("[Vending] " + hit.collider.gameObject.name + "은(는) 맞았는데 VendingMachine 컴포넌트가 없음");
                return;
            }

            Debug.Log("[Vending] " + vendingMachine.gameObject.name + " 판매 시도");
            vendingMachine.DropVendingItem();
        }
    }
}
