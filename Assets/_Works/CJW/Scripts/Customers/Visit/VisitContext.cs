using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
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

        /// <summary>단계가 바뀔 때마다 진행 상태만 되돌린다. 방문 전체 값(차·지점)은 건드리지 않는다.</summary>
        public void ResetPhaseProgress()
        {
            PhaseElapsed = 0f;
            Aligning = false;
            TargetRotation = Quaternion.identity;
        }

        public void Clear()
        {
            Customers.Clear();
            Car = null;
            ResetPhaseProgress();
        }
    }
}
