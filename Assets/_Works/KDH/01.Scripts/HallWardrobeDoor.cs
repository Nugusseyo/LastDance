using DG.Tweening;
using UnityEngine;

namespace _Works.KDH._01.Scripts
{
    /// <summary>
    /// 장롱 문(양쪽) 회전 애니메이션만 담당하는 컴포넌트.
    /// 상호작용/상태 판단은 WardrobeHideSpot이 담당하고, 이 컴포넌트는 Open/Close만 수행한다.
    /// </summary>
    public class HallWardrobeDoor : MonoBehaviour
    {
        [Header("Doors")]
        [SerializeField] private Transform doorPivotL;
        [SerializeField] private Transform doorPivotR;
        [SerializeField] private float doorOpenAngle = 95f;
        [SerializeField] private float openDuration = 0.8f;
        [SerializeField] private float closeDuration = 0.6f;
        [SerializeField] private Ease openEase = Ease.OutSine;
        [SerializeField] private Ease closeEase = Ease.InSine;

        public bool IsOpen { get; private set; }

        private Tween doorLTween;
        private Tween doorRTween;
        private Quaternion doorLClosedRot;
        private Quaternion doorRClosedRot;

        private void Awake()
        {
            if (doorPivotL != null) doorLClosedRot = doorPivotL.localRotation;
            if (doorPivotR != null) doorRClosedRot = doorPivotR.localRotation;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            PlayTween(true, openDuration, openEase);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            PlayTween(false, closeDuration, closeEase);
        }

        private void PlayTween(bool open, float duration, Ease ease)
        {
            doorLTween?.Kill();
            doorRTween?.Kill();

            // 오일러 각도(0~360 정규화) 기반 보간은 문마다 회전 방향에 따라 먼 길로
            // 돌아버릴 수 있어, 항상 최단 경로로 회전하는 쿼터니언 보간을 사용한다.
            if (doorPivotL != null)
            {
                Quaternion targetL = open
                    ? doorLClosedRot * Quaternion.Euler(0f, doorOpenAngle, 0f)
                    : doorLClosedRot;
                doorLTween = doorPivotL.DOLocalRotateQuaternion(targetL, duration).SetEase(ease);
            }

            if (doorPivotR != null)
            {
                Quaternion targetR = open
                    ? doorRClosedRot * Quaternion.Euler(0f, -doorOpenAngle, 0f)
                    : doorRClosedRot;
                doorRTween = doorPivotR.DOLocalRotateQuaternion(targetR, duration).SetEase(ease);
            }
        }
    }
}
