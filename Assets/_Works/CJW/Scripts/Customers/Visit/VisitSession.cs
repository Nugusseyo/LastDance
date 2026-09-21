using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Visit.States;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.Shared.Boarding;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>차량 1대와 그 차에 탄 손님들의 방문 한 번. 차와 손님은 서로를 참조하지 않고 이 세션이 둘을 엮는다. 단계별 진행은 VisitState가 맡고, 세션은 전이와 수명만 소유한다.</summary>
    public sealed class VisitSession : IUpdate
    {
        private const int PhaseCount = (int)VisitPhase.Completed + 1;

        private readonly VisitContext _context = new();

        /// <summary>차가 아무것도 지정하지 않았을 때 쓰는 기본 연출. 생성자가 한 번 채우고 이후 바뀌지 않는다.</summary>
        private readonly VisitState[] _defaults = new VisitState[PhaseCount];

        /// <summary>이번 방문이 실제로 쓸 연출. Begin이 매번 기본값부터 다시 세운다.</summary>
        private readonly VisitState[] _states = new VisitState[PhaseCount];

        private VisitState _current;
        /// <summary>손님 Phase 실행의 일련번호. 달려온 이전 Phase의 완료를 걸러낸다.</summary>
        private int _customerPhaseSerial;


        public VisitPhase Phase { get; private set; } = VisitPhase.None;
        public Car Car => _context.Car;
        public IReadOnlyList<AbstractCustomer> Customers => _context.Customers;

        /// <summary>Leaving까지 끝났을 때 발생. 구독자가 <see cref="ReturnToPool"/>을 호출하면 된다.</summary>
        public event Action<VisitSession> Completed;
        public event Action<VisitPhase> OnStateChanged;

        public VisitSession()
        {
            // None과 Completed는 틱이 없는 경계 단계라 상태 객체를 두지 않는다.
            AddDefault(new ArrivingState());
            AddDefault(new UnloadingState());
            AddDefault(new WaitingState());
            AddDefault(new BoardingState());
            AddDefault(new LeavingState());
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

            ApplyStateOverrides(car);

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

                // 세션이 자기 손님을 이미 알고 있으므로 전역 방송 없이 직접 물려준다.
                // 이 두 줄은 반드시 ChangeState(Arriving) 보다 앞에 와야 첫 전이를 놓치지 않는다.
                customer.BindSession(this);
                customer.Fsm?.Begin(_context, i);

                // 최초 탑승은 Phase와 무관하게 여기서 끝난다. VisitPhase.Boarding은
                // "처음 타는" 단계가 아니라 "볼일 끝나고 다시 타는" 단계다.
                Transform seat = car.HasSeat(i) ? car.GetSeat(i) : car.transform;
                if (customer.Boarding != null)
                {
                    customer.Boarding.Board(seat);
                }
                else
                {
                    Debug.LogError(
                        $"[VisitSession] {customer.name}에 탑승 모듈이 없습니다. " +
                        "프리팹에 BoardingModule을 붙여야 합니다.", customer);
                }
            }

            ChangeState(VisitPhase.Arriving);
        }

        /// <summary>세션은 풀에서 재사용되므로 매번 기본값부터 다시 세운다. 안 그러면 이전 차의 연출이 새 방문으로 샌다.</summary>
        private void ApplyStateOverrides(Car car)
        {
            Array.Copy(_defaults, _states, PhaseCount);

            VisitState[] overrides = car.Data != null ? car.Data.StateOverrides : null;
            if (overrides == null)
            {
                return;
            }

            for (int i = 0; i < overrides.Length; i++)
            {
                VisitState state = overrides[i];
                if (state == null)
                {
                    continue;
                }

                int index = (int)state.Phase;
                if (index < 0 || index >= PhaseCount)
                {
                    Debug.LogError($"[VisitSession] {car.name}의 연출 덮어쓰기에 정의되지 않은 Phase({index})가 있어 무시합니다.", car);
                    continue;
                }

                _states[index] = state;
            }
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

        /// <summary>손님을 쫓아낸다. 어느 단계에서든 부를 수 있다. 무엇이 이걸 부를지는 아직 정하지 않았다.</summary>
        public void Repel()
        {
            if (Phase is VisitPhase.None or VisitPhase.Completed or VisitPhase.Leaving)
            {
                return;
            }

            // 밖에 나와 있는 손님이 있으면 태우고 나서 보낸다. 곧장 Leaving으로 뛰면 손님이 허공에 남는다.
            ChangeState(HasCustomerOutside() ? VisitPhase.Boarding : VisitPhase.Leaving);
        }

        private bool HasCustomerOutside()
        {
            List<AbstractCustomer> customers = _context.Customers;

            for (int i = 0; i < customers.Count; i++)
            {
                IBoardable boarding = customers[i].Boarding;

                if (boarding != null && !boarding.IsBoarded)
                {
                    return true;
                }
            }

            return false;
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

                // 대기 중인 상태를 먼저 끊는다. 반납 뒤에 끊으면 한 프레임이라도 좀비가 돌 수 있다.
                customer.Fsm?.Stop();
                customer.BindSession(null);

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
            // None과 Completed는 상태가 없는 게 정상이다. 그 밖의 Phase에 상태가 없으면
            // _current가 null로 굳어 방문이 영영 끝나지 않고, 주차 자리도 반납되지 않아
            // 결국 스폰 전체가 멈춘다. 조용히 굳는 대신 방문을 닫아 자원을 회수한다.
            if (phase != VisitPhase.None && phase != VisitPhase.Completed && _states[(int)phase] == null)
            {
                Debug.LogError($"[VisitSession] {phase} 단계에 VisitState가 없어 방문을 강제로 종료합니다.");
                phase = VisitPhase.Completed;
            }

            Phase = phase;
            _current = _states[(int)phase];

            // 상태 객체는 차종끼리 공유되므로 진행 상태를 자기 필드에 들지 않는다.
            // Enter가 쓰기 전에 여기서 되돌려 준다.
            _context.ResetPhaseProgress();
            _current?.Enter(_context);

            // 세션 단계가 바뀌면 손님들에게 그 단계의 시퀀스를 돌리게 한다.
            // 세션은 "전원 끝났나"만 보면 되고, 누가 뭐를 했는지는 구별하지 않는다.
            RunCustomerPhase(phase).Forget();

            OnStateChanged?.Invoke(phase);

            if (phase == VisitPhase.Completed)
            {
                Completed?.Invoke(this);
            }
        }

        /// <summary>해당 Phase에서 손님들이 할 일을 동시에 돌리고 전원 끝날 때까지 기다린다. 세션은 손님별 소요 시간을 구별하지 않는다.</summary>
        private async UniTaskVoid RunCustomerPhase(VisitPhase phase)
        {
            // 이전 Phase의 시퀀스가 취소되면서 닫힐 때, 그 완료가 지금 Phase를
            // 끝난 것으로 표시해버리지 않도록 일련번호로 묶는다.
            int serial = ++_customerPhaseSerial;
            _context.CustomerPhaseDone = false;

            try
            {
                List<AbstractCustomer> customers = _context.Customers;
                UniTask[] running = new UniTask[customers.Count];

                for (int i = 0; i < customers.Count; i++)
                {
                    CustomerFSMModule fsm = customers[i].Fsm;
                    running[i] = fsm != null ? fsm.RunPhase(phase) : UniTask.CompletedTask;
                }

                await UniTask.WhenAll(running);
            }
            catch (OperationCanceledException)
            {
                // 방문이 중단됐다. 정상 경로라 로그하지 않는다.
            }
            catch (Exception e)
            {
                // .Forget()은 예외를 삼키므로 여기서 반드시 남긴다.
                Debug.LogException(e);
            }
            finally
            {
                if (serial == _customerPhaseSerial)
                {
                    _context.CustomerPhaseDone = true;
                }
            }
        }


        private void AddDefault(VisitState state)
        {
            _defaults[(int)state.Phase] = state;
        }
    }
}
