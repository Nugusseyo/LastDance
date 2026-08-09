using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using _Works.CJW.Scripts.Player.WashSystem.CleaningTargets;
using UnityEngine;

namespace _Works.CJW.Scripts.Player.Weapons
{
    public class CleanerCaster : MonoBehaviour
    {
        public enum CastType
        {
            Circle, Box
        }
        
        [SerializeField] private int detectCount;
        [SerializeField] private CastType castType;
        [SerializeField] private float radius; //circle only
        [SerializeField] private Vector3 boxSize; //box only
        [SerializeField] private LayerMask targetLayer;
        
        public void SetRadius(float value) => radius = value;
        public void SetBoxSize(Vector3 value) => boxSize = value;

        public IReadOnlyList<CleanableResult> CleanableResults => _cleanableResults;
        
        private CleanableResult[] _cleanableResults;
        private Collider[] _hitResults;

        private void Awake()
        {
            _cleanableResults = new CleanableResult[detectCount];
            _hitResults = new Collider[detectCount];
        }

        /// <summary>
        /// 청소 가능한 인터페이스를 감지하는 메서드
        /// </summary>
        /// <param name="direction">  direction은 Box전용. </param>
        ///
        /// <returns></returns>
        public bool CastClean(Vector3 direction = default)
        {
            Quaternion dir = Quaternion.Euler(direction);
            int cnt = castType switch
            {
                CastType.Circle => Physics.OverlapSphereNonAlloc(transform.position, radius,  _hitResults,targetLayer),
                CastType.Box => Physics.OverlapBoxNonAlloc(transform.position, boxSize, _hitResults, dir, targetLayer),
                _ => 0
            };

            for (int i = 0; i < cnt; i++)
            {
                if (_hitResults[i].TryGetComponent(out ICleanable cleanable))
                {
                    Vector2 point = _hitResults[i].ClosestPoint(transform.position);
                    CleanableResult result = new CleanableResult
                    {
                        Point = point,
                        Cleanable = cleanable
                    };
                    
                    _cleanableResults[i] = result;
                }
            }

            return cnt > 0; //카운트가 0보다 크면 맞은거니까 true
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            if(castType == CastType.Circle)
                Gizmos.DrawWireSphere(transform.position, radius);
            else if(castType == CastType.Box)
                Gizmos.DrawWireCube(transform.position, boxSize);
        }
#endif
    }

    public struct CleanableResult
    {
        public Vector3 Point;
        public ICleanable Cleanable;
    }
}