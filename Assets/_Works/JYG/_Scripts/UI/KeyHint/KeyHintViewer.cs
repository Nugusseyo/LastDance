using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.JYG._Scripts.UI.KeyHint
{
    public class KeyHintViewer : MonoBehaviour
    {
        [SerializeField] private GameObject hintPrefab;
        [SerializeField] private Transform container; // 프리팹들이 들어갈 부모 Transform (Horizontal/Vertical Layout Group 추천)

        private readonly List<GameObject> _activeHintObjects = new();

        public void CreateHint(KeyHintType type)
        {
            ClearHints();
            
            if (type == KeyHintType.None || type == 0) return;

            Transform parentTransform = container != null ? container : transform;
            
            foreach (KeyHintType singleType in Enum.GetValues(typeof(KeyHintType)))
            {
                if (singleType == KeyHintType.None) continue;
                
                if ((type & singleType) == singleType)
                {
                    GameObject itemObj = Instantiate(hintPrefab, parentTransform);
                    
                    if (itemObj.TryGetComponent(out KeyHintItemUI itemUI))
                    {
                        itemUI.SetData(singleType);
                    }

                    _activeHintObjects.Add(itemObj);
                }
            }
        }

        private void ClearHints()
        {
            for (int i = 0; i < _activeHintObjects.Count; i++)
            {
                if (_activeHintObjects[i] != null)
                {
                    Destroy(_activeHintObjects[i]);
                }
            }
            _activeHintObjects.Clear();
        }
    }
}