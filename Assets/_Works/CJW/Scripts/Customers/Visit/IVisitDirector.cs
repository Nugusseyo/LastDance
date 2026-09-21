using System;

namespace _Works.CJW.Scripts.Customers.Visit
{
    public interface IVisitDirector
    {
        int ActiveVisitCount { get; }

        /// <summary>방문이 시작될 때 발생. 세션이 이미 Arriving 단계라 Car와 Customers를 바로 읽을 수 있다.</summary>
        event Action<VisitSession> VisitStarted;

        /// <summary>주차 자리를 하나 빌리고, 차 한 대와 손님 몇 명을 꺼내 방문을 시작한다.</summary>
        VisitSession BeginVisit();
    }
}