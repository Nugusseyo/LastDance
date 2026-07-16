using System;
using System.Collections.Generic;
using JJH._02_Scripts.Agents;
using Unity.VisualScripting;
using UnityEngine;

namespace _Works.CJW.Scripts.Player
{
    public class PlayerSensor : AgentSensor
    {
        [SerializeField] private float detectAngle;
        [SerializeField] private float detectRadius;
        
        [Header("Optimization Settings")]
        [Tooltip("매 프레임 물리 연산을 하지 않고, 지정된 초(Second)마다 센싱을 수행합니다.")]
        [SerializeField] private float scanInterval = 0.1f; 
        
        private IFlashLightModule _flashLightModule;
        private float _scanTimer;

        // Double Buffering용 HashSet (O(1) 조회를 위해 HashSet 유지)
        private HashSet<IDetectable> _prevDetected = new HashSet<IDetectable>();
        private HashSet<IDetectable> _currentDetected = new HashSet<IDetectable>();

        [ContextMenu("MatchViewAngle")]
        private void MatchViewAngle()
        {
            _debugViewAngle = detectAngle;
            _debugCheckDistance = detectRadius;
        }

        private void Update()
        {
            _scanTimer += Time.deltaTime;
            if (_scanTimer < scanInterval) return;
            
            _scanTimer = 0f; // 타이머 초기화
            PerformDetection();
        }

        private void PerformDetection()
        {
            // 1. 참조 스왑 (GC Alloc 없음, O(1))
            (_prevDetected, _currentDetected) = (_currentDetected, _prevDetected);

            _currentDetected.Clear(); // 내부 버퍼 재사용 (O(1)에 수렴)

            // 2. 물리 센싱 진행
            if (_flashLightModule != null && _flashLightModule.IsActive)
            {
                if (IsTargetInSight(detectAngle, detectRadius))
                {
                    // ColliderResults는 고정 크기 배열이므로 루프 횟수가 제한됨 (O(1) 혹은 최대 고정 상수 제한)
                    int length = ColliderResults.Length;
                    for (int i = 0; i < length; i++)
                    {
                        Collider col = ColliderResults[i];
                        if (col == null) break; // ClearColliderResults로 인해 null을 만나면 이후는 볼 필요 없음

                        if (col.TryGetComponent(out IDetectable detectable))
                        {
                            _currentDetected.Add(detectable);
                            
                            // _prevDetected.Contains는 해시 테이블 탐색이므로 평균 O(1)
                            if (!_prevDetected.Contains(detectable))
                            {
                                detectable.IsDetected(true); // 새로 감지됨 (이벤트 1회 호출)
                            }
                        }
                    }
                }
            }

            // 3. 감지 해제 상태 업데이트
            // _prevDetected의 크기만큼만 루프를 돌며, Contains는 O(1)이므로 전체 시간 복잡도는 O(N)으로 보장됨
            foreach (var oldTarget in _prevDetected)
            {
                if (!_currentDetected.Contains(oldTarget))
                {
                    oldTarget.IsDetected(false); // 감지 해제됨
                }
            }
        }
    }
}