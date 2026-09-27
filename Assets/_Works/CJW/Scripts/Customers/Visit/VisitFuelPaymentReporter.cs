using System;
using System.Collections.Generic;
using _Works.JYG._Scripts.Events;
using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>주유가 끝나면 손님이 낸 돈을 <see cref="UIEvents.RefuelingEvent"/>로 쏜다. 돈을 직접 건드리지 않는다 — MoneyManager가 듣고
    /// 평판·상점 보너스를 곱해 더한다. 여기서는 차종 주유가(<see cref="MapSystems.CarDataSO.FuelPrice"/>)에 손님의 지불 배율만 곱한다.</summary>
    public class VisitFuelPaymentReporter : MonoBehaviour
    {
        /// <summary>한 방문에 걸어 둔 구독. 세션은 풀로 재사용되므로 방문이 끝나면 바로 푼다.</summary>
        private sealed class SessionHooks
        {
            public Action<AbstractCustomer> OnFueled;
            public Action<VisitSession> OnCompleted;
            public bool Paid;
        }

        [Header("참조")]
        [SerializeField] private VisitDirector visitDirector;

        [Tooltip("MoneyManager가 듣는 이벤트 채널(UIEventSO).")]
        [SerializeField] private EventChannelSO moneyChannel;

        private readonly Dictionary<VisitSession, SessionHooks> _sessions = new();

        private void OnEnable()
        {
            if (visitDirector == null || moneyChannel == null)
            {
                Debug.LogError($"[{nameof(VisitFuelPaymentReporter)}] VisitDirector와 돈 이벤트 채널을 지정해야 합니다.", this);
                return;
            }

            visitDirector.VisitStarted += OnVisitStarted;
        }

        private void OnDisable()
        {
            if (visitDirector != null)
            {
                visitDirector.VisitStarted -= OnVisitStarted;
            }

            foreach (KeyValuePair<VisitSession, SessionHooks> pair in _sessions)
            {
                Unhook(pair.Key, pair.Value);
            }

            _sessions.Clear();
        }

        private void OnVisitStarted(VisitSession session)
        {
            var hooks = new SessionHooks();
            hooks.OnFueled = customer => Pay(session, hooks, customer);
            hooks.OnCompleted = OnVisitCompleted;

            session.Fueled += hooks.OnFueled;
            session.Completed += hooks.OnCompleted;
            _sessions[session] = hooks;
        }

        private void Pay(VisitSession session, SessionHooks hooks, AbstractCustomer customer)
        {
            // 한 차는 한 번만 주유한다. 주유 손님이 여럿 알려도 돈은 한 번만 받는다.
            if (hooks.Paid)
            {
                return;
            }

            hooks.Paid = true;

            int basePrice = session.Car != null && session.Car.Data != null ? session.Car.Data.FuelPrice : 0;
            int payment = customer != null && customer.Data != null ? customer.Data.FuelPayment(basePrice) : basePrice;

            if (payment <= 0)
            {
                return;
            }

            moneyChannel.RaiseEvent(UIEvents.RefuelingEvent.Init(payment));
        }

        private void OnVisitCompleted(VisitSession session)
        {
            if (_sessions.TryGetValue(session, out SessionHooks hooks))
            {
                Unhook(session, hooks);
                _sessions.Remove(session);
            }
        }

        private static void Unhook(VisitSession session, SessionHooks hooks)
        {
            session.Fueled -= hooks.OnFueled;
            session.Completed -= hooks.OnCompleted;
        }
    }
}
