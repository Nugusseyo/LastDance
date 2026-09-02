#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DevLib.PolyNavMesh
{
    /// <summary>
    /// 타일맵을 NavMesh 폴리곤 데이터로 베이킹하는 컴포넌트.
    /// PathFinder.PathBaker에 대응하며, 격자 셀 대신 직사각형 폴리곤을 생성한다.
    ///
    /// ── 베이킹 파이프라인 ──────────────────────────────────────────────────────
    ///   1. 이동 가능한 셀 수집 (장애물 없는 ground 타일)
    ///   2. Greedy Rectangle Merge: 인접 셀을 직사각형으로 병합
    ///   3. 직사각형 → PolygonData 변환, 월드 좌표로 꼭짓점 계산
    ///   4. 인접 직사각형 간 공유 엣지 = Portal 연결
    ///   5. NavMeshData SO에 저장
    ///
    /// ── Greedy Rectangle Merge 원리 ───────────────────────────────────────────
    ///   셀을 y 오름차순으로 순회하며, 방문하지 않은 walkable 셀에서 시작해
    ///   오른쪽 → 위쪽 방향으로 최대한 확장하여 직사각형을 만든다.
    ///   최적 알고리즘(Histogram-based)보다 직사각형 수가 많을 수 있으나
    ///   구현이 단순하고 이해하기 쉽다.
    /// </summary>
    public class NavMeshBaker : MonoBehaviour
    {
        [SerializeField] private Tilemap groundMap;
        [SerializeField] private Tilemap obstacleMap;
        [SerializeField] private NavMeshData navMeshData;

        [SerializeField] private bool drawGizmos = true;
        [SerializeField] private Color polygonColor  = new Color(0.2f, 0.8f, 0.2f, 0.4f);
        [SerializeField] private Color portalColor   = Color.yellow;
        [SerializeField] private Color centerColor   = Color.green;

        [ContextMenu("Bake NavMesh")]
        private void Bake()
        {
            Debug.Assert(groundMap   != null, "groundMap is not assigned");
            Debug.Assert(obstacleMap != null, "obstacleMap is not assigned");
            Debug.Assert(navMeshData != null, "navMeshData SO is not assigned");

            navMeshData.Clear();

            HashSet<Vector3Int> walkable = CollectWalkableCells();
            List<RectInt> rects    = MergeIntoRectangles(walkable);
            BuildPolygons(rects);
            navMeshData.BuildRuntimeMap();

            Debug.Log($"[PolyNavMesh] Baked {navMeshData.polygons.Count} polygons from {walkable.Count} cells");
            SaveAsset();
        }

        // ── Step 1: 이동 가능한 셀 수집 ─────────────────────────────────────────

        private HashSet<Vector3Int> CollectWalkableCells()
        {
            var walkable = new HashSet<Vector3Int>();
            groundMap.CompressBounds();
            BoundsInt bounds = groundMap.cellBounds;

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (groundMap.HasTile(cell) && !obstacleMap.HasTile(cell))
                    walkable.Add(cell);
            }
            return walkable;
        }

        // ── Step 2: Greedy Rectangle Merge ──────────────────────────────────────

        private List<RectInt> MergeIntoRectangles(HashSet<Vector3Int> walkable)
        {
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            List<RectInt> rects   = new List<RectInt>();

            List<Vector3Int> sorted = new List<Vector3Int>(walkable);
            //Y축 우선 오름차순 정렬.
            sorted.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            foreach (Vector3Int origin in sorted)
            {
                if (visited.Contains(origin)) continue;

                // 오른쪽으로 최대 확장
                int w = 1;
                while (walkable.Contains(new Vector3Int(origin.x + w, origin.y, 0)) &&
                       !visited.Contains(new Vector3Int(origin.x + w, origin.y, 0)))
                    w++;

                // 위쪽으로 최대 확장 (행 전체가 빈 walkable 셀이어야 함)
                int h = 1;
                while (IsRowAvailable(walkable, visited, origin.x, origin.y + h, w))
                    h++;

                var rect = new RectInt(origin.x, origin.y, w, h);
                rects.Add(rect);

                //해당 사각형에 있는 모든 점을 더한다.
                for (int dx = 0; dx < w; dx++)
                    for (int dy = 0; dy < h; dy++)
                        visited.Add(new Vector3Int(origin.x + dx, origin.y + dy, 0));
            }

            return rects;
        }

        //해당행이 Rect에 포함될 수 있는지 체크하는 함수.
        private bool IsRowAvailable(HashSet<Vector3Int> walkable, HashSet<Vector3Int> visited, int startX, int y, int width)
        {
            for (int dx = 0; dx < width; dx++)
            {
                Vector3Int cell = new Vector3Int(startX + dx, y, 0);
                if (!walkable.Contains(cell) || visited.Contains(cell)) return false;
            }
            return true;
        }

        // ── Step 3: 폴리곤 생성 및 Portal 연결 ───────────────────────────────────

        private void BuildPolygons(List<RectInt> rects)
        {
            float agentRadius = navMeshData.AgentData != null ? navMeshData.AgentData.AgentRadius : 0f;

            // 직사각형 → PolygonData
            for (int i = 0; i < rects.Count; i++)
                navMeshData.polygons.Add(RectToPolygon(i, rects[i]));

            // 인접 쌍 검사 → Portal 양방향 연결
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                    TryAddPortal(rects[i], rects[j], navMeshData.polygons[i], navMeshData.polygons[j], agentRadius);
        }

        private PolygonData RectToPolygon(int id, RectInt rect)
        {
            // CellToWorld는 셀의 하단-왼쪽 모서리 좌표를 반환한다
            // 꼭짓점을 CCW(반시계 방향)로 정렬 — ContainsPoint 판별에 필요
            Vector2 bl = CellCornerToWorld(rect.xMin, rect.yMin);
            Vector2 br = CellCornerToWorld(rect.xMax, rect.yMin);
            Vector2 tr = CellCornerToWorld(rect.xMax, rect.yMax);
            Vector2 tl = CellCornerToWorld(rect.xMin, rect.yMax);

            return new PolygonData
            {
                id       = id,
                center   = (bl + br + tr + tl) * 0.25f,
                vertices = new[] { bl, br, tr, tl },  // CCW, 반시계 방향으로 정점을 넣는다.
                portals  = new List<PortalData>()
            };
        }

        /// <summary>
        /// 두 직사각형이 엣지를 공유하면 Portal을 양방향으로 추가한다.
        /// 수평 인접(좌우)과 수직 인접(상하) 두 경우를 처리한다.
        /// agentRadius만큼 포털 양 끝을 안쪽으로 줄여 에이전트가 모서리에 끼지 않게 한다.
        /// </summary>
        private void TryAddPortal(RectInt a, RectInt b, PolygonData polyA, PolygonData polyB, float agentRadius)
        {
            // 수평 인접: a의 오른쪽 = b의 왼쪽 (또는 반대)  → 포털은 Y축 방향 세그먼트
            if (a.xMax == b.xMin || b.xMax == a.xMin)
            {
                int yMin = Mathf.Max(a.yMin, b.yMin);
                int yMax = Mathf.Min(a.yMax, b.yMax);
                if (yMax <= yMin) return;  // y 축 겹침 없음

                int     px = (a.xMax == b.xMin) ? a.xMax : b.xMax;
                Vector2 pA = CellCornerToWorld(px, yMin);
                Vector2 pB = CellCornerToWorld(px, yMax);

                pA.y += agentRadius;
                pB.y -= agentRadius;
                if (pA.y >= pB.y) return;  // 포털이 너무 좁아 에이전트 통과 불가

                polyA.portals.Add(new PortalData { pointA = pA, pointB = pB, neighborId = polyB.id });
                polyB.portals.Add(new PortalData { pointA = pA, pointB = pB, neighborId = polyA.id });
            }
            // 수직 인접: a의 위 = b의 아래 (또는 반대)  → 포털은 X축 방향 세그먼트
            else if (a.yMax == b.yMin || b.yMax == a.yMin)
            {
                int xMin = Mathf.Max(a.xMin, b.xMin);
                int xMax = Mathf.Min(a.xMax, b.xMax);
                if (xMax <= xMin) return;  // x 축 겹침 없음

                int     py = (a.yMax == b.yMin) ? a.yMax : b.yMax;
                Vector2 pA = CellCornerToWorld(xMin, py);
                Vector2 pB = CellCornerToWorld(xMax, py);

                pA.x += agentRadius;
                pB.x -= agentRadius;
                if (pA.x >= pB.x) return;  // 포털이 너무 좁아 에이전트 통과 불가

                polyA.portals.Add(new PortalData { pointA = pA, pointB = pB, neighborId = polyB.id });
                polyB.portals.Add(new PortalData { pointA = pA, pointB = pB, neighborId = polyA.id });
            }
        }

        /// <summary>
        /// 셀 격자 좌표 (정수 모서리)를 월드 좌표로 변환한다.
        /// CellToWorld는 셀 중심이 아닌 셀 경계를 기준으로 한다.
        /// </summary>
        private Vector2 CellCornerToWorld(int x, int y)
            => (Vector2)groundMap.CellToWorld(new Vector3Int(x, y, 0));

        private void SaveAsset()
        {
#if UNITY_EDITOR
            EditorUtility.SetDirty(navMeshData);
            AssetDatabase.SaveAssets();
#endif
        }

        // ── Gizmos ───────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || navMeshData == null) return;

            foreach (var poly in navMeshData.polygons)
            {
                // 폴리곤 윤곽선
                Gizmos.color = polygonColor;
                Handles.color = polygonColor;
                DrawPolygonGizmo(poly.vertices);

                // 중심점
                Gizmos.color = centerColor;
                Gizmos.DrawWireSphere(poly.center, 0.1f);

                // Portal 엣지
                Gizmos.color = portalColor;
                foreach (var portal in poly.portals)
                {
                    Gizmos.DrawLine(portal.pointA, portal.pointB);
                    Gizmos.DrawWireSphere((portal.pointA + portal.pointB) * 0.5f, 0.2f);
                }
            }
        }

        private static void DrawPolygonGizmo(Vector2[] verts)
        {
            for (int i = 0; i < verts.Length; i++)
            {
                Handles.DrawLine(verts[i], verts[(i + 1) % verts.Length], 4f);
            }
        }
#endif
    }
}
