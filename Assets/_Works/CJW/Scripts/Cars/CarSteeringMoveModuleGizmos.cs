#if UNITY_EDITOR
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>
    /// 에디터 전용 표시. 씬 뷰에서 차를 선택하면 보인다.
    ///   파란 구 두 개 — 최소 회전 반경
    ///   노란 선      — 남은 경로 (하늘색이면 마지막 직선 구간)
    ///   빨간 선      — 지금 겨누는 목표점. 이 선의 길이가 곧 Lookahead다.
    /// </summary>
    public partial class CarSteeringMoveModule
    {
        private void OnValidate()
        {
            if (_lookaheadMax < _lookaheadMin)
            {
                _lookaheadMax = _lookaheadMin;
            }
        }

        private void OnDrawGizmosSelected()
        {
            float radius = MinTurnRadius;

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + transform.right * radius, 0.15f);
            Gizmos.DrawWireSphere(transform.position - transform.right * radius, 0.15f);

            if (!Application.isPlaying || _path == null || !_path.HasPath)
            {
                return;
            }

            Vector3[] corners = _path.Corners;

            Gizmos.color = _path.OnFinalLeg ? Color.cyan : Color.yellow;
            for (int i = _path.SegmentIndex; i < _path.CornerCount - 1; i++)
            {
                Gizmos.DrawLine(corners[i], corners[i + 1]);
            }

            Vector3 cursor = _path.Advance(transform.position, out _);
            Vector3 goal = _path.FindGoalPoint(cursor, CurrentLookahead());

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, goal);
            Gizmos.DrawWireSphere(goal, 0.3f);
        }
    }
}
#endif
