using System.Collections.Generic;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.StoreUI
{
    public class UpgradeBarInitializer : MonoBehaviour
    {
        [SerializeField] private GameObject barPrefab;
        private List<SpriteRenderer> _barRendererList = new List<SpriteRenderer>();

        [Header("Color Settings")] 
        [SerializeField] private Color defaultColor;
        [SerializeField] private Color upgradeColor;
        
        #region Property

        public List<SpriteRenderer> BarRendererList => _barRendererList;
        
        #endregion
        public void InitializeBar(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                _barRendererList.Add(Instantiate(barPrefab, transform).GetComponent<SpriteRenderer>());
            }
        }

        public void SetColor(int index, bool isUpgrade)
        {
            if (index >= _barRendererList.Count)
            {
                Debug.LogWarning("레벨보다 더 큰 레벨값을 전달했습니다. : " + gameObject.name);
                return;
            }

            int maxCount = _barRendererList.Count - 1;
            _barRendererList[maxCount - index].color = isUpgrade ? upgradeColor : defaultColor;
        }
    }
}
