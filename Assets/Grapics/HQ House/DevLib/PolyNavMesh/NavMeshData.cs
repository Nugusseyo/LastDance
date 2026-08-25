using System.Collections.Generic;
using UnityEngine;

namespace DevLib.PolyNavMesh
{
    /// <summary>
    /// 베이킹된 NavMesh 폴리곤 데이터를 저장하는 ScriptableObject.
    /// PathFinder의 BakedDataSo에 대응한다.
    ///
    /// polygons 리스트는 직렬화되어 에셋으로 저장되고,
    /// 런타임에는 빠른 조회를 위해 Dictionary로 변환된다.
    /// </summary>
    [CreateAssetMenu(fileName = "NavMeshData", menuName = "Lib/PolyNavMesh/NavMeshData")]
    public class NavMeshData : ScriptableObject
    {
        [field: SerializeField] public PolyNavAgentDataSo AgentData { get; private set; }
        public List<PolygonData> polygons = new List<PolygonData>();

        // 런타임 전용 — ID → NavPolygon 빠른 조회
        private Dictionary<int, NavPolygon> _runtimeMap;

        private void OnEnable() => BuildRuntimeMap();

        /// <summary>
        /// 직렬화 데이터로부터 런타임 딕셔너리를 빌드한다.
        /// NavMeshBaker가 베이킹을 완료한 뒤에도 호출된다.
        /// </summary>
        public void BuildRuntimeMap()
        {
            _runtimeMap = new Dictionary<int, NavPolygon>(polygons.Count);
            
            foreach (var data in polygons)
            {
                _runtimeMap[data.id] = new NavPolygon
                {
                    id       = data.id,
                    center   = data.center,
                    vertices = data.vertices,
                    portals  = data.portals
                };
            }
            Debug.Log($"[NavMesh] Building runtime map with {polygons.Count} polygons");
        }

        /// <summary>
        /// 월드 좌표를 포함하는 폴리곤을 반환한다. O(n).
        /// (Unity NavMesh는 내부적으로 공간 분할 자료구조를 사용해 O(log n)에 처리한다)
        /// </summary>
        public bool GetPolygonAt(Vector2 worldPoint, out NavPolygon polygon)
        {
            foreach (var p in _runtimeMap.Values)
            {
                if (ContainsPoint(p.vertices, worldPoint))
                {
                    polygon = p;
                    return true;
                }
            }
            polygon = null;
            return false;
        }

        public bool TryGetPolygon(int id, out NavPolygon polygon)
        {
            polygon = null;
            return _runtimeMap != null && _runtimeMap.TryGetValue(id, out polygon);
        }

        /// <summary>
        /// 월드 좌표와 가장 가까운 폴리곤(중심 기준)을 반환한다.
        /// 목적지가 NavMesh 밖일 때 부분 경로의 종착 폴리곤을 찾는 데 사용된다.
        /// </summary>
        public bool GetNearestPolygon(Vector2 worldPoint, out NavPolygon polygon)
        {
            polygon = null;
            if (_runtimeMap == null || _runtimeMap.Count == 0) return false;

            float bestSqr = float.MaxValue;
            foreach (var p in _runtimeMap.Values)
            {
                float sqr = (p.center - worldPoint).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; polygon = p; }
            }
            return polygon != null;
        }
            

        public void Clear() => polygons.Clear();

        // ── 볼록 폴리곤 내부 판별 (Cross Product 방식) ───────────────────────────
        // CCW 순서로 정렬된 볼록 폴리곤에서, 모든 엣지에 대해 점이 왼쪽에 있으면 내부.
        private static bool ContainsPoint(Vector2[] verts, Vector2 p)
        {
            for (int i = 0, j = verts.Length - 1; i < verts.Length; j = i++)
            {
                Vector2 a = verts[j], b = verts[i];
                // Cross(b-a, p-a): 음수면 p가 엣지의 오른쪽(외부)
                float cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
                if (cross < 0f) return false;
            }
            return true;
        }
    }
}
