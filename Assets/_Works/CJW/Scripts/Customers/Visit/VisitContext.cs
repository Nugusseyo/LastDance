using System.Collections.Generic;
using _Works.CJW.Scripts.Customers.Cars;
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
        public Vector3 ShopPoint;
        public Vector3 ExitPoint;
        public float Interval;

        public void Clear()
        {
            Customers.Clear();
            Car = null;
        }
    }
}
