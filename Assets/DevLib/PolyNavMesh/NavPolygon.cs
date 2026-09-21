using System.Collections.Generic;
using UnityEngine;

namespace DevLib.PolyNavMesh
{
    /// <summary>
    /// 런타임에 사용하는 폴리곤 정적 데이터.
    ///
    /// PathFinder의 NodeData(셀 단위 정적 데이터)에 대응한다.
    /// A* 탐색 상태(G, F, parent)는 NavPolygon이 아닌 별도의 AStarNode에 저장하므로,
    /// 여러 에이전트가 동시에 탐색해도 이 객체는 수정되지 않는다.
    /// </summary>
    public class NavPolygon
    {
        public int id;
        public Vector2 center;
        public Vector2[] vertices;
        public List<PortalData> portals;

        public override bool Equals(object obj) => obj is NavPolygon p && p.id == id;
        public override int GetHashCode() => id;

        public static bool operator ==(NavPolygon a, NavPolygon b)
        {
            if (a is null) return b is null;
            return a.Equals(b);
        }
        public static bool operator !=(NavPolygon a, NavPolygon b) => !(a == b);
    }
}
