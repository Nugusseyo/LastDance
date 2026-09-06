using UnityEngine;
using UnityEngine.InputSystem;
using _Works.KDH._01.Scripts.Car;

// E키를 누르면 지금 들고 있는 부품을 판다
public class PartSellInput : MonoBehaviour
{
    [SerializeField] private PartDetacher partDetacher;
    [SerializeField] private PartSeller partSeller;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            GameObject heldPart = partDetacher.HeldPart;

            int price = partSeller.SellPart(heldPart);

            if (price > 0)
            {
                Debug.Log("부품을 팔았다! 가격: " + price);
            }
        }
    }
}
