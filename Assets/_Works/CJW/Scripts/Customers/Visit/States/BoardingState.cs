using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>손님을 차로 불러 좌석 순서대로 태운다.</summary>
    public sealed class BoardingState : IVisitState
    {
        private int _cursor;

        public VisitPhase Phase => VisitPhase.Boarding;

        public void Enter(VisitContext context)
        {
            _cursor = 0;

            Vector3 carPosition = context.Car.transform.position;
            for (int i = 0; i < context.Customers.Count; i++)
            {
                context.Customers[i].MoveTo(carPosition);
            }
        }

        public VisitPhase Tick(VisitContext context, float dt)
        {
            if (_cursor >= context.Customers.Count)
            {
                return VisitPhase.Leaving;
            }

            // 도착한 순서가 아니라 좌석 순서대로 태워 좌석 배정을 단순하게 유지한다.
            AbstractCustomer customer = context.Customers[_cursor];
            if (!customer.IsArrived)
            {
                return VisitPhase.Boarding;
            }

            // 좌석이 모자라면 차 원점에 붙인다. 어긋난 사실은 Begin에서 이미 에러로 남겼다.
            customer.Board(context.Car.HasSeat(_cursor) ? context.Car.GetSeat(_cursor) : context.Car.transform);
            _cursor++;

            return _cursor >= context.Customers.Count ? VisitPhase.Leaving : VisitPhase.Boarding;
        }
    }
}
