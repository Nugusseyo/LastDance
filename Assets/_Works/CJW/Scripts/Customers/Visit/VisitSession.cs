using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Visit.States;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>
    /// 차량 1대와 그 차에 탄 손님들의 방문 한 번.
    /// 차와 손님은 서로를 참조하지 않고, 둘을 엮는 일은 전부 이 세션 안에서만 일어난다.
    /// 단계별 진행은 IVisitState가 맡고, 세션은 전이와 수명만 소유한다.
    /// </summary>
    public sealed class VisitSession : IUpdate
    {
        private const int PhaseCount = (int)VisitPhase.Completed + 1;

        private readonly VisitContext _context = new();
        private readonly IVisitState[] _states = new IVisitState[PhaseCount];

        private IVisitState _current;

        public VisitPhase Phase { get; private set; } = VisitPhase.None;
        public Car Car => _context.Car;
        public IReadOnlyList<AbstractCustomer> Customers => _context.Customers;

        /// <summary>Leaving까지 끝났을 때 발생. 구독자가 <see cref="ReturnToPool"/>을 호출하면 된다.</summary>
        public event Action<VisitSession> Completed;

        public VisitSession()
        {
            // None과 Completed는 틱이 없는 경계 단계라 상태 객체를 두지 않는다.
            AddState(new ArrivingState());
            AddState(new UnloadingState());
            AddState(new WaitingState());
            AddState(new BoardingState());
            AddState(new LeavingState());
        }
        /// <param name="arrivalPoint">차량이 정차할 위치.</param>
        /// <param name="arrivalRotation">정차했을 때 차가 바라볼 방향.</param>
        /// <param name="shopPoint">하차한 손님이 향할 가게 안 위치.</param>
        /// <param name="exitPoint">방문이 끝난 차량이 빠져나갈 위치.</param>
        /// <remarks>손님 한 명씩 처리할 때의 간격은 차의 CarDataSO에서 온다.</remarks>
        public void Begin(Car car, IReadOnlyList<AbstractCustomer> customers,
                          Vector3 arrivalPoint, Quaternion arrivalRotation,
                          Vector3 shopPoint, Vector3 exitPoint)
        {
            _context.Clear();
            _context.Car = car;
            _context.ArrivalPoint = arrivalPoint;
            _context.ArrivalRotation = arrivalRotation;
            _context.ShopPoint = shopPoint;
            _context.ExitPoint = exitPoint;
            _context.Interval = car.BoardingInterval;

            if (customers.Count > car.SeatCount)
            {
                // 목록에서 빼면 반납이 안 돼 손님이 허공에 남는다. 태우기는 하되 문제를 크게 남긴다.
                Debug.LogError(
                    $"[VisitSession] {car.name}의 좌석은 {car.SeatCount}개인데 손님이 {customers.Count}명입니다. " +
                    "남는 손님은 차 원점에 겹쳐 앉습니다. CarDataSO의 인원 범위를 확인하세요.");
            }

            for (int i = 0; i < customers.Count; i++)
            {
                AbstractCustomer customer = customers[i];
                _context.Customers.Add(customer);
                customer.Board(car.HasSeat(i) ? car.GetSeat(i) : car.transform);
            }

            ChangeState(VisitPhase.Arriving);
        }

        /// <summary>가게 볼일이 끝나 손님들을 태워 보낼 때 호출한다.</summary>
        public void RequestDeparture()
        {
            if (Phase != VisitPhase.Waiting)
            {
                Debug.LogWarning($"[VisitSession] {Phase} 단계에서는 출발을 요청할 수 없습니다.");
                return;
            }

            ChangeState(VisitPhase.Boarding);
        }

        public void OnUpdate(float dt)
        {
            if (_current == null)
            {
                return;
            }

            VisitPhase next = _current.Tick(_context, dt);
            if (next != _current.Phase)
            {
                ChangeState(next);
            }
        }

        /// <summary>손님을 먼저, 차를 나중에 반납한다. 순서가 뒤집히면 손님이 허공에 남는다.</summary>
        public void ReturnToPool(PoolManagerSO pool)
        {
            List<AbstractCustomer> customers = _context.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                AbstractCustomer customer = customers[i];
                customer.transform.SetParent(null, true);
                pool.Push(customer);
            }

            pool.Push(_context.Car);

            _context.Clear();
            _current = null;
            Phase = VisitPhase.None;
        }

        private void ChangeState(VisitPhase phase)
        {
            Phase = phase;
            _current = _states[(int)phase];
            _current?.Enter(_context);

            if (phase == VisitPhase.Completed)
            {
                Completed?.Invoke(this);
            }
        }

        private void AddState(IVisitState state)
        {
            _states[(int)state.Phase] = state;
        }
    }
}
