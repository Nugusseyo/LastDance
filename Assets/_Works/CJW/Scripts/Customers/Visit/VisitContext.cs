using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>
    /// 방문 단계들이 공유하는 값. 단계별 진행 상태(커서, 타이머)는 각 상태가 스스로 들고 있다.
    /// </summary>
    public sealed class VisitContext
    {
        public readonly List<AbstractCustomer> Customers = new();

        public Car Car;
        public Vector3 ArrivalPoint;
        /// <summary>정차했을 때 차가 바라볼 방향. 주차 자리의 회전이 그대로 들어온다.</summary>
        public Quaternion ArrivalRotation = Quaternion.identity;
        public Vector3 ShopPoint;
        public Vector3 ExitPoint;
        public float Interval;
        /// <summary>
        /// 현재 Phase의 손님별 시퀀스가 전원 끝났는지.
        /// 세션 상태는 동기 Tick이고 손님 머신은 비동기라, 둘을 잇는 다리가 이 플래그다.
        /// IVisitState를 async로 바꾸면 이 플래그는 사라지고 await가 그 자리를 대신한다.
        /// </summary>
        public bool CustomerPhaseDone = true;


        public void Clear()
        {
            Customers.Clear();
            Car = null;
        }
    }
}
