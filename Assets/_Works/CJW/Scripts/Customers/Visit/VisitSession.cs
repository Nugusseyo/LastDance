using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States;
using _Works.CJW.Scripts.Customers.Visit.States;
using _Works.CJW.Scripts.MapSystems;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.JJH._02_Scripts.Objects;
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

        /// <summary>주유를 받고 나서 출발하기까지 최소로 두는 시간(초). 주유가 끝난 걸 눈으로 본 뒤에 떠나게 한다.</summary>
        private const float DepartAfterFuelDelay = 1.5f;

        /// <summary>주유 뒤 손님이 할 일(대사 등)을 마치길 기다리는 최대 시간(초). 넘기면 하던 일을 끊고 출발한다.</summary>
        private const float DepartAfterFuelMaxWait = 15f;

        /// <summary>이번 방문에서 주유를 받은 손님.</summary>
        private readonly HashSet<AbstractCustomer> _fueled = new();

        /// <summary>주유가 다 끝나고 흐른 시간(초). 음수면 기다리는 중이 아니다.</summary>
        private float _departAfterFuelTimer = -1f;

        /// <summary>주유를 기다리던 손님이 죽어 이 차가 더 머물 이유가 없다. Waiting에 들어서는 대로 곧장 출발시킨다.</summary>
        private bool _departWhenWaiting;

        /// <summary>이번 방문 차의 주유구. 손님이 주유기에 닿기 전에 주유가 끝나도 기록해 두려고 방문 내내 듣는다.</summary>
        private FuelDoor _fuelDoor;

        public VisitPhase Phase { get; private set; } = VisitPhase.None;
        public Car Car => _context.Car;
        public IReadOnlyList<AbstractCustomer> Customers => _context.Customers;

        /// <summary>이 방문의 차가 버려졌는지. 손님이 다른 차를 훔쳐 떠났거나, 탄 사람이 모두 죽었을 때다.
        /// 방문을 닫을 때 차를 풀로 돌리지 않고 자리도 비우지 않는다.</summary>
        public bool IsCarAbandoned => _context.Abandoning;

        /// <summary>손님이 훔쳐 탄 차. 버려진 방문이 아니면 null이다.</summary>
        public StealableCar StolenCar => _context.StolenCar;

        /// <summary>Leaving까지 끝났을 때 발생. 구독자가 <see cref="ReturnToPool"/>을 호출하면 된다.</summary>
        public event Action<VisitSession> Completed;
        public event Action<VisitPhase> OnStateChanged;

        /// <summary>이 방문의 손님이 주유를 받았다.</summary>
        public event Action<AbstractCustomer> Fueled
        {
            add => _context.Fueled += value;
            remove => _context.Fueled -= value;
        }

        /// <summary>이 방문의 손님이 주유를 너무 오래 기다렸다.</summary>
        public event Action<AbstractCustomer> FuelLate
        {
            add => _context.FuelLate += value;
            remove => _context.FuelLate -= value;
        }

        public VisitSession()
        {
            // None과 Completed는 틱이 없는 경계 단계라 상태 객체를 두지 않는다.
            AddDefault(new ArrivingState());
            AddDefault(new UnloadingState());
            AddDefault(new WaitingState());
            AddDefault(new BoardingState());
            AddDefault(new LeavingState());

            // 컨텍스트는 세션과 수명이 같아 한 번만 구독한다.
            _context.Fueled += HandleFueled;
        }

        /// <param name="arrivalPoint">차량이 정차할 위치.</param>
        /// <param name="arrivalRotation">정차했을 때 차가 바라볼 방향.</param>
        /// <param name="shopPoint">하차한 손님이 향할 가게 안 위치.</param>
        /// <param name="exitPoint">방문이 끝난 차량이 빠져나갈 위치.</param>
        /// <param name="mapData">주차 자리·주유기 지점. 퇴장하는 차가 줄 사이를 가로지르지 않게 본다.</param>
        /// <remarks>손님 한 명씩 처리할 때의 간격은 차의 CarDataSO에서 온다.</remarks>
        public void Begin(Car car, IReadOnlyList<AbstractCustomer> customers,
                          Vector3 arrivalPoint, Quaternion arrivalRotation,
                          Vector3 shopPoint, Vector3 exitPoint, MapDataSo mapData = null)
        {
            _context.Clear();
            ResetFuelDeparture();
            _context.Car = car;
            _context.ArrivalPoint = arrivalPoint;
            _context.ArrivalRotation = arrivalRotation;
            _context.ShopPoint = shopPoint;
            _context.ExitPoint = exitPoint;
            _context.MapData = mapData;
            _context.Interval = car.BoardingInterval;
            ListenFuelDoor(car);

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

        /// <summary>손님이 다른 차를 훔치러 나설 때 부른다. 이때부터 출발·퇴치 요청을 무시해, 훔치는 도중에 자기 차로 불려 가지 않게 한다.</summary>
        public void BeginAbandon(StealableCar stolen)
        {
            _context.Abandoning = true;
            _context.StolenCar = stolen;
        }

        /// <summary>훔치러 가다 실패했을 때 부른다. 평소 흐름(자기 차로 돌아와 떠나기)으로 되돌린다.</summary>
        public void CancelAbandon()
        {
            _context.Abandoning = false;
            _context.StolenCar = null;
        }

        /// <summary>훔친 차가 맵을 빠져나갔을 때 부른다. 부른 손님의 상태가 끝난 뒤인 다음 틱에 방문을 닫는다.</summary>
        public void FinishAbandon()
        {
            if (_context.Abandoning)
            {
                _context.AbandonDone = true;
            }
        }

        /// <summary>가게 볼일이 끝나 손님들을 태워 보낼 때 호출한다.</summary>
        public void RequestDeparture()
        {
            // 손님이 차를 훔쳐 떠나는 중이다. 부르면 훔치던 손님이 자기 차로 되돌아온다.
            if (_context.Abandoning)
            {
                return;
            }

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
            if (_context.Abandoning || Phase is VisitPhase.None or VisitPhase.Completed or VisitPhase.Leaving)
            {
                return;
            }

            // 밖에 나와 있는 손님이 있으면 태우고 나서 보낸다. 곧장 Leaving으로 뛰면 손님이 허공에 남는다.
            ChangeState(HasCustomerOutside() ? VisitPhase.Boarding : VisitPhase.Leaving);
        }

        /// <summary>방문에서 손님 하나를 뺀다. 죽은 손님처럼 더는 차에 태울 수 없는 손님에게 쓴다.
        /// 뺀 손님의 행동은 끊고, 풀 반납은 부른 쪽이 한다 — 세션은 이 손님을 다시 건드리지 않는다.
        /// 남은 손님이 있으면 그들만 태우고 평소대로 떠난다. 아무도 안 남으면 차는 떠나지 않고 버려진 차로 자리에 남는다.</summary>
        public bool Remove(AbstractCustomer customer)
        {
            if (customer == null || !_context.Customers.Remove(customer))
            {
                return false;
            }

            // 행동을 끊기 전에 읽는다. 주유를 받기 전에 빠졌는지가 곧장 출발할지를 정한다.
            bool waitedForFuel = customer.Fsm != null && customer.Fsm.WantsFuel && !_fueled.Contains(customer);
            _fueled.Remove(customer);

            // 행동을 끊으면 이 손님의 Phase 실행이 곧바로 끝나, 세션이 이 손님을 기다리지 않는다.
            customer.Fsm?.Stop();
            customer.BindSession(null);

            if (_context.Customers.Count == 0)
            {
                AbandonEmptyCar();
            }
            else if (waitedForFuel && !AnyoneWaitingForFuel())
            {
                // 주유를 받으러 온 손님이 죽었다. 남은 일행이 주유 대기 안전망(수 분)을 다 채울 때까지 서 있지 않게 바로 떠나보낸다.
                _departAfterFuelTimer = -1f;
                _departWhenWaiting = true;
            }

            return true;
        }

        /// <summary>남은 손님 중 아직 주유를 받지 못한 주유 손님이 있는지.</summary>
        private bool AnyoneWaitingForFuel()
        {
            List<AbstractCustomer> customers = _context.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                CustomerFSMModule fsm = customers[i].Fsm;
                if (fsm != null && fsm.WantsFuel && !_fueled.Contains(customers[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>탈 사람이 모두 사라진(죽은) 차를 그 자리에 버린다. 방문은 다음 틱에 닫히고, 차는 훔쳐 간 손님이 버린 차처럼
        /// 치울 때까지(<see cref="VisitDirector.ClearAbandonedCar"/>) 주차 자리를 차지한 채 멈춰 있다.</summary>
        private void AbandonEmptyCar()
        {
            // 이미 떠나는 중이거나 닫힌 방문은 건드리지 않는다. 차를 훔쳐 가는 중이면 그 흐름이 방문을 닫는다.
            if (_context.Abandoning || Phase is VisitPhase.None or VisitPhase.Completed or VisitPhase.Leaving)
            {
                return;
            }

            _context.Abandoning = true;
            _context.AbandonDone = true;

            // 자리를 맞추던 중이었을 수 있다. 방문이 닫히면 차는 틱을 받지 않으니, 여기서 세워 두지 않으면 하던 움직임 그대로 굳는다.
            _context.Car?.Stop();
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

            // 손님이 훔친 차로 떠났다. 버려진 차는 자리에 남기고 방문만 닫는다.
            if (_context.AbandonDone)
            {
                ChangeState(VisitPhase.Completed);
                return;
            }

            TickDepartWhenWaiting();
            TickFuelDeparture(dt);
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

                // 풀은 꺼낼 때 ResetItem을 부르므로 반납만으로는 말풍선이 접히지 않는다. 여기서 접지 않으면 떠난 손님의 말이 허공에 남는다.
                customer.Fsm?.Context?.EndSpeech();
                customer.BindSession(null);

                customer.transform.SetParent(null, true);
                pool.Push(customer);
            }

            // 버려진 차는 자리에 그대로 남긴다. 치우는 쪽(VisitDirector.ClearAbandonedCar)이 나중에 반납한다.
            if (!_context.Abandoning)
            {
                pool.Push(_context.Car);
            }

            _context.Clear();
            ResetFuelDeparture();
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

            // 떠나기 시작하면 머물면서 하던 말("가득이요" 같은 요구)은 끝난 것이다. 접지 않으면 말풍선을 단 채 차에 타고 떠난다.
            // 탑승 단계에서 새로 하는 말은 아래 시퀀스가 이 뒤에 띄운다.
            if (phase == VisitPhase.Boarding)
            {
                EndAllSpeech();
            }

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


        /// <summary>주유를 원하던 손님이 모두 주유를 받으면 잠시 뒤 출발한다. 안 그러면 주유가 끝나도 손님이 그 자리에 선 채
        /// 자동 출발 타이머나 Waiting 한계 시간이 다 될 때까지 아무 반응 없이 기다린다.</summary>
        private void HandleFueled(AbstractCustomer customer)
        {
            if (customer == null || !_fueled.Add(customer))
            {
                return;
            }

            List<AbstractCustomer> customers = _context.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                CustomerFSMModule fsm = customers[i].Fsm;
                if (fsm != null && fsm.WantsFuel && !_fueled.Contains(customers[i]))
                {
                    return;
                }
            }

            _departAfterFuelTimer = 0f;
        }

        /// <summary>주유 손님이 죽어 걸어 둔 출발을 낸다. 아직 내리는 중(Unloading)이면 Waiting에 들어설 때까지 들고 있다.</summary>
        private void TickDepartWhenWaiting()
        {
            if (!_departWhenWaiting)
            {
                return;
            }

            // 퇴치로 이미 떠나는 중이거나, 손님이 차를 훔쳐 가는 중이면 끼어들 필요가 없다.
            if (_context.Abandoning || Phase is VisitPhase.Boarding or VisitPhase.Leaving or VisitPhase.Completed or VisitPhase.None)
            {
                _departWhenWaiting = false;
                return;
            }

            if (Phase == VisitPhase.Waiting)
            {
                _departWhenWaiting = false;
                RequestDeparture();
            }
        }

        private void TickFuelDeparture(float dt)
        {
            if (_departAfterFuelTimer < 0f)
            {
                return;
            }

            _departAfterFuelTimer += dt;
            if (_departAfterFuelTimer < DepartAfterFuelDelay)
            {
                return;
            }

            // 주유 뒤에 대사 같은 할 일이 남은 손님이 있으면 마칠 때까지 기다린다. 출발하면 하던 일이 끊긴다.
            if (_departAfterFuelTimer < DepartAfterFuelMaxWait && !AllFueledCustomersIdle())
            {
                return;
            }

            _departAfterFuelTimer = -1f;

            // 그새 퇴치나 차 도난으로 흐름이 바뀌었으면 끼어들지 않는다.
            if (Phase == VisitPhase.Waiting && !_context.Abandoning)
            {
                RequestDeparture();
            }
        }

        /// <summary>주유 받은 손님이 모두 이번 단계에서 할 일을 마치고 출발만 기다리는지.</summary>
        private bool AllFueledCustomersIdle()
        {
            foreach (AbstractCustomer customer in _fueled)
            {
                CustomerState current = customer != null ? customer.Fsm?.Machine?.Current : null;
                if (current != null && !(current is StayState stay && stay.IsIndefinite))
                {
                    return false;
                }
            }

            return true;
        }

        private void EndAllSpeech()
        {
            List<AbstractCustomer> customers = _context.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                customers[i].Fsm?.Context?.EndSpeech();
            }
        }

        private void ResetFuelDeparture()
        {
            _fueled.Clear();
            _departAfterFuelTimer = -1f;
            _departWhenWaiting = false;
            ListenFuelDoor(null);
        }

        /// <summary>차의 주유구를 갈아 끼운다. 차는 풀에서 재사용되므로 이전 방문의 구독을 반드시 푼다.</summary>
        private void ListenFuelDoor(Car car)
        {
            if (_fuelDoor != null)
            {
                _fuelDoor.OnFuelingCompleted -= HandleCarFueled;
            }

            _fuelDoor = car != null ? car.GetComponentInChildren<FuelDoor>(true) : null;

            if (_fuelDoor != null)
            {
                _fuelDoor.OnFuelingCompleted += HandleCarFueled;
            }
        }

        private void HandleCarFueled()
        {
            _context.CarFueled = true;
        }

        private void AddDefault(VisitState state)
        {
            _defaults[(int)state.Phase] = state;
        }
    }
}
