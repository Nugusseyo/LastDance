using System.Collections.Generic;
using System.Linq;
using _Works.JYG._Scripts.Data_Container.Money;
using _Works.JYG._Scripts.UI.Data.Money;
using _Works.JYG._Scripts.Util;
using DG.Tweening;
using Resources.DataBase.Review_Data;
using UnityEngine;
using UnityEngine.Pool;

namespace _Works.JYG._Scripts.UI.Data.Review
{
    public class ReviewEffector : MonoBehaviour //Money Effector와 하는 일이 굉장히 유사하다. 나중에 추상 부모를 받게 하자. 
    {
        [SerializeField] private ReviewItem reviewPrefab;
        [SerializeField] private Transform poolParent;
        private IObjectPool<ReviewItem> _reviewPool;
        
        [Header("DOTween Setting")]
        [SerializeField] private float duration = 1f;
        [SerializeField] private Ease easing = Ease.OutBack;
        [SerializeField] private float waitTime = 3f;

        private ReviewDB _reviewDB;

        private void Awake()
        {
            _reviewDB = UnityEngine.Resources.Load<ReviewDB>("DataBase/Review Data/ReviewDB");
            if (_reviewDB == null)
            {
                Debug.LogError("ReviewDB not Found. 경로 이탈 의심. 코드 비활성화 됨");
                enabled = false;
                return;
            }
            
            _reviewPool = new ObjectPool<ReviewItem>(
                HandleCreateViewer,
                HandleGetViewer,
                HandleReleaseViewer,
                HandleDestroyViewer,
                true,
                30,
                100
                );
        }

        public void RequestRandomReview(ReviewType type, int index)
        {
            ReviewItem item = _reviewPool.Get();

            string userName = "익명";

            if (_reviewDB != null && _reviewDB.NameSheet != null && _reviewDB.NameSheet.Count > 0)
            {
                var randomNameData = _reviewDB.NameSheet
                    [Random.Range(0, _reviewDB.NameSheet.Count)];
                
                userName = randomNameData?.name ?? "NameDB Content is Null";
            }
            
            ReviewData data = _reviewDB.ReviewSheet.Find(x => x.index == index);
            
            string content = type switch
            {
                ReviewType.Good => data.contentUp,
                ReviewType.Bad => data.contentDown,
                ReviewType.Late => data.contentLate,
                _ => string.Empty
            };

            int starCount = type switch
            {
                ReviewType.Good => 5,
                ReviewType.Bad => 1,
                ReviewType.Late => 3,
                _ => 4
            };
            
            item.SetReview(userName, content, starCount);
        }
        
        #region Pooling
        
        private ReviewItem HandleCreateViewer()
        {
            return Instantiate(reviewPrefab, poolParent);
        }

        private void HandleGetViewer(ReviewItem obj)
        {
            RectTransform contentRect = obj.transform.GetChild(0) as RectTransform;
            if (contentRect == null) return;

            contentRect.DOKill();
            float width = contentRect.rect.width;
            
            contentRect.anchoredPosition = new Vector2(width, contentRect.anchoredPosition.y);
            obj.gameObject.SetActive(true);

            Sequence seq = DOTween.Sequence();
            seq.SetTarget(contentRect); //Sequence의 DOTween을 대상 타겟의 DOTween판정으로 옮겨준다.
            
            seq.Append(contentRect.DOAnchorPosX(0f, duration).SetEase(easing));
            seq.AppendInterval(waitTime);
            seq.Append(contentRect.DOAnchorPosX(width, duration).SetEase(Ease.InBack));
            seq.OnComplete(() => _reviewPool.Release(obj));
            seq.SetUpdate(true);
        }

        private void HandleReleaseViewer(ReviewItem obj)
        {
            obj.transform.DOKill();
            obj.gameObject.SetActive(false);
        }
        private void HandleDestroyViewer(ReviewItem obj)
        {
            obj.transform.DOKill();
            Destroy(obj.gameObject);
        }
        #endregion
    }
}
