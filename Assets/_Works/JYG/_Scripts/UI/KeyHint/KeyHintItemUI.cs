using TMPro;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.KeyHint
{
    public class KeyHintItemUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI keyText;    // 예: [E], [F], [RMB]
        [SerializeField] private TextMeshProUGUI actionText; // 예: 사용하기, 총 줍기

        public void SetData(KeyHintType type)
        {
            // KeyHintType에 따른 키 및 안내문 세팅
            switch (type)
            {
                case KeyHintType.UsingItem:
                    SetText("E", "아이템 사용");
                    break;
                case KeyHintType.PickUpGun:
                    SetText("F", "아이템/주유건 집기");
                    break;
                case KeyHintType.SellingCar:
                    SetText("E", "차량 판매");
                    break;
                case KeyHintType.Interact:
                    SetText("E", "상호작용");
                    break;
                case KeyHintType.RemoveWheel:
                    SetText("F", "바퀴 해체");
                    break;
            }
        }

        private void SetText(string key, string action)
        {
            if (keyText != null) keyText.text = key;
            if (actionText != null) actionText.text = action;
        }
    }
}