using System;
using System.Collections.Generic;
using UnityEngine;

namespace DevLib.PolyNavMesh
{
    /// <summary>
    /// NavMesh를 구성하는 하나의 볼록 폴리곤 데이터 (직렬화용).
    ///
    /// PathFinder의 NodeData에 대응하며, 셀 하나가 아닌 '병합된 직사각형 영역' 하나를 나타낸다.
    /// Unity NavMesh에서 Navigation Polygon(삼각형 메시의 한 면)에 해당한다.
    ///
    /// 타일맵 베이킹 결과로 생성되며, 인접 폴리곤과 Portal로 연결된다.
    /// </summary>
    [Serializable]
    public class PolygonData
    {
        public int id;
        public Vector2 center;       // 폴리곤 중심점 (월드 좌표) — A* 휴리스틱 계산에 사용
        public Vector2[] vertices;   // 꼭짓점 배열, CCW 순서 (직사각형이면 4개)
        public List<PortalData> portals = new List<PortalData>();  // 인접 폴리곤 연결 목록
    }
}
