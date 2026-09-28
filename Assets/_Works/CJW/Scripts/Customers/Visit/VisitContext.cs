using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>방문 단계들이 공유하는 값. 단계의 진행 상태도 여기 있다 —
    /// VisitState 인스턴스는 CarDataSO에 꽂혀 동시 방문 여러 개가 나눠 쓰므로 자기 필드에 진행을 담을 수 없다.</summary>
    public sealed class VisitContext
    {
        public readonly List<AbstractCustomer> Customers = new();

        public Car Car;
        public Vector3 ArrivalPoint;
        /// <summary>정차했을 때 차가 바라볼 방향. 주차 자리의 회전이 그대로 들어온다.</summary>
        public Quaternion ArrivalRotation = Quaternion.identity;
        public Vector3 ShopPoint;
        public Vector3 ExitPoint;
        /// <summary>주차 자리·주유기 같은 맵 지점. 차가 빠져나갈 길을 고를 때 본다. 없을 수 있다.</summary>
        public MapDataSo MapData;
        public float Interval;
        /// <summary>현재 Phase의 손님별 시퀀스가 전원 끝났는지. 동기 Tick인 세션 상태와 비동기 손님 머신을 잇는 다리다.</summary>
        public bool CustomerPhaseDone = true;

        /// <summary>현재 단계에 들어온 뒤 흐른 시간(초). VisitSession.ChangeState가 Enter 직전에 0으로 되돌린다.</summary>
        public float PhaseElapsed;

        /// <summary>Arriving 전용. 도착해서 멈춘 뒤, 남은 각도를 마저 맞추는 중인지.</summary>
        public bool Aligning;

        /// <summary>Arriving 전용. 이번 주차에서 실제로 맞출 방향. 전면·후면 주차를 둘 다 허용하므로
        /// 자리 회전 그대로일 수도, 180도 뒤집힌 것일 수도 있다.</summary>
        public Quaternion TargetRotation = Quaternion.identity;

        /// <summary>순회 연출이 지금 몇 번째 경유지를 향하고 있는지. 상태 인스턴스를 동시 방문 여러 대가 나눠 쓰므로
        /// 커서도 방문마다 따로 있어야 한다 — 상태의 필드에 두면 두 대가 서로의 순서를 밀어낸다.</summary>
        public int PatrolIndex;

        /// <summary>지금 경유지를 향해 달린 시간(초). 길이 막힌 경유지에서 매 프레임 다음으로 넘어가며 헛도는 걸 막는다.</summary>
        public float PatrolPointElapsed;

        /// <summary>Leaving 전용. 자리에서 곧게 빠져나오는 중인지. 빠져나온 뒤에 퇴장 지점으로 목적지를 바꾼다.</summary>
        public bool Departing;

        /// <summary>Leaving 전용. 곧게 빠져나올 지점.</summary>
        public Vector3 DepartPoint;

        /// <summary>Leaving 전용. 차가 마지막으로 앞으로 나아간 자리와, 그 뒤로 막히지 않은 채 제자리였던 시간(초).</summary>
        public Vector3 LeaveProgressPoint;
        public float LeaveStallElapsed;

        /// <summary>Leaving 전용. 제자리에 멈춘 차를 다시 출발시킨 횟수.</summary>
        public int LeaveRestarts;

        /// <summary>Leaving 전용. 앞차에 막힌 채 마지막 경적 뒤로 기다린 시간(초).</summary>
        public float LeaveHonkElapsed;

        /// <summary>Leaving 전용. 서 있는 앞차에 막혀 제자리인 채 마지막 탈출 계획 뒤로 흐른 시간(초).</summary>
        public float LeaveEscapeElapsed;

        /// <summary>손님이 다른 차를 훔치러 나섰는지. 이때부터 방문은 출발·퇴치 요청을 받지 않고 손님이 떠나기를 기다린다.</summary>
        public bool Abandoning;

        /// <summary>훔친 차가 맵을 빠져나갔는지. 세션이 다음 틱에 방문을 닫는다.</summary>
        public bool AbandonDone;

        /// <summary>손님이 훔쳐 탄 차. 방문을 닫을 때 손님을 먼저 풀로 돌린 뒤 치운다.</summary>
        public StealableCar StolenCar;

        /// <summary>손님이 주유를 받았다. 손님 상태가 알리고, 평판처럼 방문 밖에서 결과를 셈하는 쪽이 듣는다.</summary>
        public event Action<AbstractCustomer> Fueled;

        /// <summary>손님이 주유를 너무 오래 기다렸다. 한 번 기다림에 한 번만 온다.</summary>
        public event Action<AbstractCustomer> FuelLate;

        /// <summary>이 방문의 차가 주유를 다 받았는지. 손님이 주유기에 닿기 전에 주유가 끝나도 놓치지 않도록 신호 대신 이 값을 본다.</summary>
        public bool CarFueled;

        public void ReportFueled(AbstractCustomer customer) => Raise(Fueled, customer);

        public void ReportFuelLate(AbstractCustomer customer) => Raise(FuelLate, customer);

        /// <summary>손님이 주유를 기다리다 포기했다. 방문은 주유를 받은 것과 똑같이 보고 일행을 태워 떠난다. 평판은 먼저 온 <see cref="FuelLate"/>가 깎는다.</summary>
        public event Action<AbstractCustomer> FuelGaveUp;

        public void ReportFuelGaveUp(AbstractCustomer customer) => Raise(FuelGaveUp, customer);

        /// <summary>듣는 쪽(평판·UI)이 예외를 던져도 알린 손님의 행동까지 끊기지 않게 여기서 받아 남긴다.
        /// 그대로 흘리면 주유를 기다리던 상태가 예외로 끝나, 그 뒤 주유를 해 줘도 아무도 듣지 않는다.</summary>
        private static void Raise(Action<AbstractCustomer> handlers, AbstractCustomer customer)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<AbstractCustomer>)handler).Invoke(customer);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>단계가 바뀔 때마다 진행 상태만 되돌린다. 방문 전체 값(차·지점)은 건드리지 않는다.</summary>
        public void ResetPhaseProgress()
        {
            PhaseElapsed = 0f;
            Aligning = false;
            TargetRotation = Quaternion.identity;
            PatrolIndex = 0;
            PatrolPointElapsed = 0f;
            Departing = false;
            DepartPoint = Vector3.zero;
            LeaveProgressPoint = Vector3.zero;
            LeaveStallElapsed = 0f;
            LeaveRestarts = 0;
            LeaveHonkElapsed = 0f;
            LeaveEscapeElapsed = 0f;
        }

        public void Clear()
        {
            Customers.Clear();
            Car = null;
            Abandoning = false;
            AbandonDone = false;
            StolenCar = null;
            CarFueled = false;
            ResetPhaseProgress();
        }
    }
}
