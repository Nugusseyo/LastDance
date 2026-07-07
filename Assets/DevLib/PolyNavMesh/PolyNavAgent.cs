using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DevLib.ModuleSystem;
using UnityEngine;

namespace DevLib.PolyNavMesh
{
    /// <summary>
    /// 폴리곤 기반 NavMesh를 이용한 경로 탐색 에이전트.
    /// PathFinder.PathAgent와 동일한 인터페이스를 제공하나,
    /// 내부적으로 다음 두 단계를 거친다.
    ///
    ///   Phase 1 — A* on Polygons
    ///     PathFinder의 A*가 셀 단위로 탐색하듯, 폴리곤 단위로 "어느 폴리곤을 밟을지" 결정한다.
    ///     노드 수가 훨씬 적어 대규모 맵에서 압도적으로 빠르다.
    ///
    ///   Phase 2 — Funnel (String Pulling)
    ///     폴리곤 시퀀스 → Portal 목록 추출 → 실제 꺾임점(waypoint) 계산.
    ///     PathFinder의 코너 추출(방향 변화 감지)보다 정확하고 최단 경로를 보장한다.
    ///
    /// ── 스레드 안전성 ──────────────────────────────────────────────────────────
    /// AStarNode는 탐색마다 새로 생성되므로 여러 에이전트가 동시에 사용해도 안전하다.
    /// navMeshData의 런타임 딕셔너리는 베이킹 이후 읽기 전용이므로 백그라운드 스레드에서
    /// 안전하게 읽을 수 있다.
    /// </summary>
    public class PolyNavAgent : Module
    {
        [SerializeField] private NavMeshData navMeshData;

        private CancellationTokenSource _cts = new CancellationTokenSource();
        private bool    _isCalculating;
        private Vector2 _lastDestination;

        public bool PathPending   => _isCalculating;
        public bool HasPath       { get; private set; }
        public bool IsPathStale   { get; private set; }
        /// <summary>목적지에 완전히 도달할 수 없어 최근접 지점까지만 경로를 찾은 경우 true.</summary>
        public bool IsPartialPath { get; private set; }

        public void InvalidatePath()
        {
            if (HasPath) IsPathStale = true;
        }

        /// <summary>
        /// 비동기 경로 탐색. 결과 waypoint를 pointArr에 채우고 포인트 수를 반환한다.
        /// 실패 또는 취소 시 -1 반환.
        /// </summary>
        public async Task<int> GetPath(Vector2 start, Vector2 destination, Vector2[] pointArr)
        {
            if (_isCalculating)
                _cts?.Cancel();

            if (HasPath && (destination - _lastDestination).sqrMagnitude > 0.001f)
                IsPathStale = true;

            if (_cts is null or { IsCancellationRequested: true })
                _cts = new CancellationTokenSource();

            CancellationToken token = _cts.Token;

            try
            {
                _isCalculating = true;

                // WebGL은 싱글스레드 환경이라 Task.Run(Thread Pool) 사용 불가.
                // Task.FromResult로 동기 실행하되, async Task 인터페이스는 유지한다.
                Task<(List<Vector2>, PathStatus)> pathTask;
#if UNITY_WEBGL
                pathTask = Task.FromResult(CalculatePath(start, destination, token));
#else
                pathTask = Task.Run(() => CalculatePath(start, destination, token), token);
#endif
                (List<Vector2> waypoints, PathStatus status) = await pathTask;

                int count = 0;
                if (status != PathStatus.Invalid)
                {
                    for (int i = 0; i < waypoints.Count && i < pointArr.Length; i++, count++)
                        pointArr[i] = waypoints[i];

                    HasPath          = true;
                    IsPathStale      = false;
                    IsPartialPath    = status == PathStatus.Partial;
                    _lastDestination = destination;
                }
                else
                {
                    HasPath       = false;
                    IsPathStale   = false;
                    IsPartialPath = false;
                }

                return status != PathStatus.Invalid ? count : -1;
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
                HasPath     = false;
                IsPathStale = false;
                return -1;
            }
            finally
            {
                _isCalculating = false;
            }
        }

        // ── 백그라운드 탐색 로직 ─────────────────────────────────────────────────

        private enum PathStatus { Complete, Partial, Invalid }

        private (List<Vector2> path, PathStatus status) CalculatePath(Vector2 start, Vector2 end, CancellationToken ct)
        {
            if (!navMeshData.GetPolygonAt(start, out NavPolygon startPoly))
                return (null, PathStatus.Invalid);

            bool destOutside = !navMeshData.GetPolygonAt(end, out NavPolygon endPoly);
            if (destOutside)
            {
                // 목적지가 NavMesh 밖: 가장 가까운 폴리곤을 종착점으로 사용
                if (!navMeshData.GetNearestPolygon(end, out endPoly))
                    return (null, PathStatus.Invalid);
            }

            // 같은 폴리곤이면 직선으로 이동 가능
            if (startPoly == endPoly)
            {
                Vector2 effectiveEnd = destOutside ? endPoly.center : end;
                PathStatus samePolyStatus = destOutside ? PathStatus.Partial : PathStatus.Complete;
                return (new List<Vector2> { start, effectiveEnd }, samePolyStatus);
            }

            // Phase 1: A* — 폴리곤 시퀀스 탐색 (도달 불가 시 최근접 폴리곤까지 부분 경로 반환)
            (List<NavPolygon> polyPath, bool isPartialAStar) = AStarPolygons(startPoly, endPoly, ct);
            if (polyPath == null) return (null, PathStatus.Invalid);

            // Phase 2: Portal 추출
            List<(Vector2, Vector2)> portals = ExtractOrientedPortals(polyPath);

            // 부분 경로일 때 Funnel 종착점은 마지막 도달 폴리곤의 중심
            bool isPartial = isPartialAStar || destOutside;
            Vector2 funnelEnd = isPartial ? polyPath[polyPath.Count - 1].center : end;

            // Phase 3: Funnel — 실제 waypoint 계산
            float agentRadius = navMeshData.AgentData != null ? navMeshData.AgentData.AgentRadius : 0f;
            List<Vector2> waypoints = FunnelAlgorithm.StringPull(start, funnelEnd, portals, agentRadius);
            return (waypoints, isPartial ? PathStatus.Partial : PathStatus.Complete);
        }

        // ── Phase 1: A* on Polygons ──────────────────────────────────────────────
        //
        // PathFinder.CalculatePath와 구조가 동일하나,
        // 탐색 단위가 '셀'이 아닌 '폴리곤'이라는 점이 다르다.
        // G값 계산에 폴리곤 중심 간 거리를 사용하고,
        // H값(휴리스틱)도 목표 폴리곤 중심까지의 유클리드 거리를 사용한다.

        // isPartial=true: 목표에 도달하지 못했으나 최근접 노드까지의 경로를 반환
        private (List<NavPolygon> path, bool isPartial) AStarPolygons(NavPolygon start, NavPolygon end, CancellationToken ct)
        {
            var startNode = new AStarNode(start) { G = 0, F = CalcH(start, end) };

            var openList  = new MinHeap<AStarNode>((a, b) => a.F.CompareTo(b.F));
            var closedSet = new HashSet<int>();

            openList.Push(startNode);

            // 목표에 가장 가까이 도달한 노드를 추적 (부분 경로용)
            AStarNode bestNode = startNode;
            float     bestH    = CalcH(start, end);

            while (openList.Count > 0)
            {
                if (ct.IsCancellationRequested) return (null, false);

                AStarNode current = openList.Pop();
                if (closedSet.Contains(current.polygon.id)) continue;
                closedSet.Add(current.polygon.id);

                if (current.polygon == end)
                    return (ReconstructPolyPath(current), false);

                float h = CalcH(current.polygon, end);
                if (h < bestH) { bestH = h; bestNode = current; }

                foreach (var portal in current.polygon.portals)
                {
                    if (closedSet.Contains(portal.neighborId)) continue;
                    if (!navMeshData.TryGetPolygon(portal.neighborId, out NavPolygon neighbor)) continue;

                    float newG = current.G + Vector2.Distance(current.polygon.center, neighbor.center);

                    AStarNode existing = openList.Find(n => n.polygon.id == portal.neighborId);
                    if (existing != null)
                    {
                        // Decrease-Key: 더 짧은 경로 발견 시 힙 순서 복구
                        if (newG < existing.G)
                        {
                            existing.G      = newG;
                            existing.F      = newG + CalcH(neighbor, end);
                            existing.parent = current;
                            openList.DecreaseKey(existing);
                        }
                    }
                    else
                    {
                        openList.Push(new AStarNode(neighbor)
                        {
                            G      = newG,
                            F      = newG + CalcH(neighbor, end),
                            parent = current
                        });
                    }
                }
            }

            // 오픈리스트 소진: 목표 불가. bestNode까지의 부분 경로 반환
            return (ReconstructPolyPath(bestNode), true);
        }

        private static List<NavPolygon> ReconstructPolyPath(AStarNode end)
        {
            var path = new List<NavPolygon>();
            for (AStarNode n = end; n != null; n = n.parent)
                path.Add(n.polygon);
            path.Reverse();
            return path; //폴리곤 경로를 역으로 배열.
        }

        private static float CalcH(NavPolygon a, NavPolygon b)
            => Vector2.Distance(a.center, b.center);

        // ── Phase 2: Portal 추출 ─────────────────────────────────────────────────
        //
        // 폴리곤 시퀀스에서 각 인접 폴리곤 쌍의 공유 Portal을 찾고,
        // 이동 방향(from.center → to.center) 기준으로 left/right를 결정한다.
        //
        // 결정 방법: 방향 벡터 dir와 포털 중점 mid를 기준으로
        // Cross(dir, pointA - mid) > 0 이면 pointA가 왼쪽.
        // (2D Cross Product: dir.x*(p.y-mid.y) - dir.y*(p.x-mid.x))

        private static List<(Vector2 left, Vector2 right)> ExtractOrientedPortals(List<NavPolygon> polyPath)
        {
            var portals = new List<(Vector2 left, Vector2 right)>(polyPath.Count - 1);

            for (int i = 0; i < polyPath.Count - 1; i++)
            {
                NavPolygon from = polyPath[i];
                NavPolygon to   = polyPath[i + 1];

                PortalData? found = null;
                foreach (PortalData p in from.portals)
                {
                    if (p.neighborId == to.id) { found = p; break; }
                }
                if (found == null) continue;

                Vector2 pA  = found.Value.pointA;
                Vector2 pB  = found.Value.pointB;
                Vector2 dir = to.center - from.center;
                Vector2 mid = (pA + pB) * 0.5f;

                // cross > 0 이면 pA가 이동 방향의 왼쪽 중심에서 pa로 가는 벡터와 방향간의 외적의 z값만 가져왔다.
                // (A_y * B_z - A_z * B_y, A_z * B_x - A_x * B_z, A_x * B_y - A_y * B_x)
                float cross = dir.x * (pA.y - mid.y) - dir.y * (pA.x - mid.x);
                portals.Add(cross >= 0f ? (pA, pB) : (pB, pA));
            }

            return portals;
        }

        // ── A* 탐색용 내부 노드 ──────────────────────────────────────────────────
        //
        // PathFinder의 AstarNode에 대응.
        // NavPolygon(정적 데이터)을 수정하지 않고 탐색 상태를 별도로 관리한다.

        private sealed class AStarNode
        {
            public readonly NavPolygon polygon;
            public float G;
            public float F;
            public AStarNode parent;

            public AStarNode(NavPolygon polygon) => this.polygon = polygon;
        }
    }
}
