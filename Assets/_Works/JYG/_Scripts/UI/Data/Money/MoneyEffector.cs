using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.Util;
using DevLib.ObjectPool.Runtime;
using UnityEngine;
using UnityEngine.Pool;

namespace _Works.JYG._Scripts.UI.Data.Money
{
    public class MoneyEffector : MonoBehaviour
    {
        [SerializeField] private IntegerDataContainer moneyManager;
        [SerializeField] private MoneyTextViewer moneyViewerPrefab;
        [SerializeField] private Transform textViewerParent;
        private IObjectPool<MoneyTextViewer> _moneyTextViewerPool;

        [SerializeField] private Transform moveGoal;
        [SerializeField] private Vector3 textOffset;
        
        private const string PLUS = "+";

        private void Awake()
        {
            if (moneyManager != null)
                moneyManager.OnValueChanged += HandleValueChanged;

            _moneyTextViewerPool = new ObjectPool<MoneyTextViewer>(
                HandleCreateViewer,
                HandleGetViewer,
                HandleReleaseViewer,
                HandleDestroyViewer,
                true,
                30,
                100
                );
        }

        private void OnDestroy()
        {
            if(moneyManager != null)
                moneyManager.OnValueChanged -= HandleValueChanged;
        }
        
        private void HandleValueChanged(int newValue, int oldValue)
        {
            MoneyTextViewer viewer = _moneyTextViewerPool.Get();
            int gap = newValue - oldValue;
            bool isPlus = gap >= 0;
            
            string text = TextConvert.Get(gap);
            if (isPlus) text = PLUS + text;
            
            viewer.SetText(text, isPlus);
            viewer.MoveToGoal(() => _moneyTextViewerPool.Release(viewer));
        }
        
        #region Pooling
        
        private MoneyTextViewer HandleCreateViewer()
        {
            MoneyTextViewer textViewer 
                = Instantiate(moneyViewerPrefab, textViewerParent)
                    .GetComponent<MoneyTextViewer>();
            
            textViewer.transform.SetParent(transform);
            textViewer.InitializeViewer(moveGoal);
            return textViewer;
        }

        private void HandleGetViewer(MoneyTextViewer obj)
        {
            obj.transform.position = moveGoal.position + textOffset;
            obj.gameObject.SetActive(true);
        }

        private void HandleReleaseViewer(MoneyTextViewer obj)
        {
            obj.gameObject.SetActive(false);
        }
        private void HandleDestroyViewer(MoneyTextViewer obj)
        {
            Destroy(obj.gameObject);
        }
        #endregion
    }
}
