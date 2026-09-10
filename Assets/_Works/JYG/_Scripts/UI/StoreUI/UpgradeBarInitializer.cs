using System.Collections.Generic;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class UpgradeBarInitializer : MonoBehaviour
    {
        [SerializeField] private GameObject barPrefab;                              //만들어질 바 프리팹
        private List<SpriteRenderer> _barRendererList = new List<SpriteRenderer>(); //Bar(레벨 칸)을 만들고, 만들어진 바의 렌더러를 담는다.

        [Header("Color Settings")] 
        [SerializeField] private Color defaultColor;    //레벨업이 안된 상태의 색상이다.
        [SerializeField] private Color upgradeColor;    //레벨업 시 바뀔 색상이다.
        
        #region Property

        public Color DefaultColor => defaultColor;  //업그레이드 시 올라갈 바를 미리 보여주는 작업을 하기 위해서
        public Color UpgradeColor => upgradeColor;  //Upgrade와 Def컬러를 받을 수 있도록 프로퍼티로 묶어주었다.
        
        #endregion
        public void InitializeBar(int count)    //갯수만큼 Bar를 만들어낸다.
        {
            for (int i = 0; i < count; ++i)
            {
                _barRendererList.Add(Instantiate(barPrefab, transform).GetComponent<SpriteRenderer>());
            }
        }

        public void SetColor(int index, bool isUpgrade) //업그레이드 시 해당 함수를 호출해 비주얼을 바꾸어줄 수 있다.
        {
            if (index >= _barRendererList.Count)
            {
                Debug.LogWarning("레벨보다 더 큰 레벨값을 전달했습니다. : " + gameObject.name);
                index = _barRendererList.Count - 1;
            }

            int maxCount = _barRendererList.Count - 1;
            _barRendererList[maxCount - index].color = isUpgrade ? upgradeColor : defaultColor;
        }
    }
}
