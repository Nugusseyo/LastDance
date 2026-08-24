using System.Collections.Generic;
using UnityEngine;

namespace DevLib.PolyNavMesh
{
    /// <summary>
    /// Funnel 알고리즘 (String Pulling) 구현.
    ///
    /// ── 역할 ──────────────────────────────────────────────────────────────────
    /// A*가 "어느 폴리곤들을 밟을지"를 결정한다면,
    /// Funnel 알고리즘은 그 폴리곤 시퀀스로부터 실제 이동 경로(꺾임점)를 추출한다.
    ///
    /// ── 원리 ──────────────────────────────────────────────────────────────────
    /// 경로를 실(string)이라고 생각하고 출발점과 목표점 사이에 팽팽하게 당겼을 때,
    /// 실이 걸리는 폴리곤 모서리(= Portal 끝점)가 waypoint가 된다.
    ///
    /// 구체적으로는 출발점에서 목표점 방향으로 각 포털을 통과할 때
    /// 시야 범위(funnel = 깔때기)를 좁혀가며 꺾어야 하는 지점을 찾는다.
    ///
    ///   apex ──────────────────────────────── end
    ///          \   portal1  \  portal2  \
    ///           L ──────────R ──────────
    ///
    /// ── 참고 ──────────────────────────────────────────────────────────────────
    /// Mikko Mononen의 Recast/Detour (오픈소스 NavMesh 라이브러리)의
    /// Simple Stupid Funnel Algorithm을 2D용으로 구현한 것이다.
    /// </summary>
    public static class FunnelAlgorithm
    {
        /// <summary>
        /// 포털 시퀀스와 시작/끝 점으로부터 실제 이동 경로(waypoints)를 추출한다.
        /// </summary>
        /// <param name="start">시작 월드 좌표</param>
        /// <param name="end">목표 월드 좌표</param>
        /// <param name="portals">
        ///   A* 폴리곤 시퀀스에서 추출한 포털 목록.
        ///   각 포털은 (left, right) 튜플로, 이동 방향 기준 왼쪽/오른쪽 끝점을 의미한다.
        /// </param>
        public static List<Vector2> StringPull(
            Vector2 start,
            Vector2 end,
            List<(Vector2 left, Vector2 right)> portals,
            float agentRadius = 0f)
        {
            var path = new List<Vector2> { start };

            if (portals.Count == 0)
            {
                path.Add(end);
                return path;
            }

            // 시작점과 목표점을 포털 목록에 포함시켜 통합 처리
            var pts = new List<(Vector2 left, Vector2 right)>(portals.Count + 2);
            pts.Add((start, start));  // [0] apex 초기값
            pts.AddRange(portals);
            pts.Add((end, end));      // [n+1] 목표 포털

            // 깔때기(funnel) 상태
            Vector2 apex       = start;
            Vector2 portalLeft = start;
            Vector2 portalRight = start;
            int apexIdx  = 0;
            int leftIdx  = 0;
            int rightIdx = 0;

            for (int i = 1; i < pts.Count; i++)
            {
                (Vector2 newLeft, Vector2 newRight) = pts[i]; //구조 분해 할당.

                // ── 오른쪽 경계 갱신 ──────────────────────────────────────────
                // TriArea2 >= 0 이면 newRight가 apex→portalRight 의 왼쪽 = 깔때기 안쪽
                // (Unity Y-up: TriArea2 양수 = 왼쪽. 오른쪽 경계 안쪽 = 경계선보다 왼쪽)
                if (TriArea2(apex, portalRight, newRight) >= 0f)
                {
                    // 깔때기가 좁아지거나 유지됨 → 오른쪽 경계를 newRight로 갱신
                    if (apex == portalRight || TriArea2(apex, portalLeft, newRight) < 0f) // newRight가 왼쪽 경계보다 오른쪽(안쪽)
                    {
                        portalRight = newRight;
                        rightIdx    = i;
                    }
                    else
                    {
                        // newRight가 왼쪽 경계를 넘어섬 → 왼쪽 모서리가 waypoint
                        // "실이 왼쪽 모서리에 걸림"
                        if (portalLeft != apex)
                        {
                            // 접근 방향의 CW 수직(오른쪽)으로 오프셋 — 왼쪽 벽에서 이격
                            Vector2 d = (portalLeft - apex).normalized;
                            path.Add(portalLeft + new Vector2(d.y, -d.x) * agentRadius);
                        }

                        // apex를 왼쪽 모서리로 전진시키고, 해당 포털부터 다시 계산
                        apex       = portalLeft;
                        apexIdx    = leftIdx;
                        portalLeft = apex;
                        portalRight = apex;
                        leftIdx    = apexIdx;
                        rightIdx   = apexIdx;
                        i          = apexIdx;
                        continue;
                    }
                }

                // ── 왼쪽 경계 갱신 ──────────────────────────────────────────
                // TriArea2 <= 0 이면 newLeft가 apex→portalLeft 의 오른쪽 = 깔때기 안쪽
                // (Unity Y-up: TriArea2 음수 = 오른쪽. 왼쪽 경계 안쪽 = 경계선보다 오른쪽)
                if (TriArea2(apex, portalLeft, newLeft) <= 0f)
                {
                    // 깔때기가 좁아지거나 유지됨 → 왼쪽 경계를 newLeft로 갱신
                    if (apex == portalLeft || TriArea2(apex, portalRight, newLeft) > 0f) // newLeft가 오른쪽 경계보다 왼쪽(안쪽)
                    {
                        portalLeft = newLeft;
                        leftIdx    = i;
                    }
                    else
                    {
                        // newLeft가 오른쪽 경계를 넘어섬 → 오른쪽 모서리가 waypoint
                        // "실이 오른쪽 모서리에 걸림"
                        if (portalRight != apex)
                        {
                            // 접근 방향의 CCW 수직(왼쪽)으로 오프셋 — 오른쪽 벽에서 이격
                            Vector2 d = (portalRight - apex).normalized;
                            path.Add(portalRight + new Vector2(-d.y, d.x) * agentRadius);
                        }

                        // apex를 오른쪽 모서리로 전진시키고, 해당 포털부터 다시 계산
                        apex       = portalRight;
                        apexIdx    = rightIdx;
                        portalLeft = apex;
                        portalRight = apex;
                        leftIdx    = apexIdx;
                        rightIdx   = apexIdx;
                        i          = apexIdx;
                        continue;
                    }
                }
            }

            // 마지막 목표점 추가 (이미 추가된 경우 제외)
            if (path.Count == 0 || path[^1] != end)
                path.Add(end);

            return path;
        }

        /// <summary>
        /// 삼각형 (a, b, c)의 부호 있는 넓이 × 2 (2D Cross Product).
        /// 양수: c가 벡터 a→b 의 왼쪽에 있음 (CCW)
        /// 음수: c가 벡터 a→b 의 오른쪽에 있음 (CW)
        /// 0   : a, b, c가 일직선
        /// </summary>
        private static float TriArea2(Vector2 a, Vector2 b, Vector2 c)
        {  
            Vector2 ba = b - a;
            Vector2 ca = c - a;
            return ba.x * ca.y - ba.y * ca.x;
        }
    }
}
