using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Customers.Health;
using _Works.JYG._Scripts.Events;
using _Works.Shared.Combat;
using DevLib.EventChannelSystem;
using Resources.DataBase.Human_Data;
using Resources.DataBase.Review_Data;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit
{
    /// <summary>방문에서 일어난 일을 평판으로 바꿔 <see cref="UIEvents.ReviewEvent"/>로 쏜다. 평판 값을 직접 건드리지 않는다 — ReviewManager가 듣고 더한다.
    /// 주유 결과는 방문(<see cref="VisitSession.Fueled"/>/<see cref="VisitSession.FuelLate"/>)에서, 퇴치·폭행은 손님 체력 이벤트에서 듣는다.</summary>
    public class VisitReputationReporter : MonoBehaviour
    {
        /// <summary>한 손님에 걸어 둔 구독. 풀로 재사용되는 손님이라 방문이 끝나거나 죽으면 바로 풀어야 한다.</summary>
        private sealed class Tracked
        {
            public AbstractCustomer Customer;
            public ICustomerHealth Health;
            public Action<HitInfo> OnDamaged;
            public Action<HitInfo> OnDied;
            public bool Defeated;
        }

        private sealed class SessionHooks
        {
            public readonly List<Tracked> Customers = new();
            public Action<AbstractCustomer> OnFueled;
            public Action<AbstractCustomer> OnFuelLate;
            public Action<VisitSession> OnCompleted;
        }

        [Header("참조")]
        [SerializeField] private VisitDirector visitDirector;
        [Tooltip("ReviewManager가 듣는 이벤트 채널.")]
        [SerializeField] private EventChannelSO reviewChannel;

        [Header("평판 변화량")]
        [Tooltip("주유를 해 줬을 때.")]
        [SerializeField] private int fueled = 2;
        [Tooltip("주유가 너무 늦어졌을 때.")]
        [SerializeField] private int fuelLate = -1;
        [Tooltip("진상(HumanType.Bad)을 퇴치했을 때.")]
        [SerializeField] private int badDefeated = 5;
        [Tooltip("진상을 퇴치하지 못하고 방문이 끝났을 때.")]
        [SerializeField] private int badMissed = -3;
        [Tooltip("일반인(HumanType.Good)을 때렸을 때. 한 대마다.")]
        [SerializeField] private int goodHit = -3;

        private readonly Dictionary<VisitSession, SessionHooks> _sessions = new();

        private void OnEnable()
        {
            if (visitDirector == null || reviewChannel == null)
            {
                Debug.LogError($"[{nameof(VisitReputationReporter)}] VisitDirector와 평판 이벤트 채널을 지정해야 합니다.", this);
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
            hooks.OnFueled = _ => Raise(fueled, ReviewType.Good);
            hooks.OnFuelLate = _ => Raise(fuelLate, ReviewType.Late);
            hooks.OnCompleted = OnVisitCompleted;

            session.Fueled += hooks.OnFueled;
            session.FuelLate += hooks.OnFuelLate;
            session.Completed += hooks.OnCompleted;

            IReadOnlyList<AbstractCustomer> customers = session.Customers;
            for (int i = 0; i < customers.Count; i++)
            {
                Track(hooks, customers[i]);
            }

            _sessions[session] = hooks;
        }

        private void Track(SessionHooks hooks, AbstractCustomer customer)
        {
            ICustomerHealth health = customer != null ? customer.Health : null;
            var tracked = new Tracked { Customer = customer, Health = health };

            if (health != null)
            {
                tracked.OnDamaged = hit => OnDamaged(tracked, hit);
                tracked.OnDied = _ => OnDied(tracked);
                health.Damaged += tracked.OnDamaged;
                health.Died += tracked.OnDied;
            }

            hooks.Customers.Add(tracked);
        }

        private void OnDamaged(Tracked tracked, HitInfo hit)
        {
            // 다른 손님이 때린 건(싸움꾼 등) 플레이어 탓이 아니다.
            if (tracked.Customer.HumanType == HumanType.Good && !IsFromCustomer(hit))
            {
                Raise(goodHit, ReviewType.Bad);
            }
        }

        private void OnDied(Tracked tracked)
        {
            if (tracked.Customer.HumanType == HumanType.Bad)
            {
                Raise(badDefeated, ReviewType.Good);
            }

            // 죽은 손님은 방문에서 빠져 잠시 뒤 풀로 돌아간다. 다른 방문에 다시 나오기 전에 구독을 푼다.
            tracked.Defeated = true;
            Untrack(tracked);
        }

        private void OnVisitCompleted(VisitSession session)
        {
            if (!_sessions.TryGetValue(session, out SessionHooks hooks))
            {
                return;
            }

            for (int i = 0; i < hooks.Customers.Count; i++)
            {
                Tracked tracked = hooks.Customers[i];
                if (!tracked.Defeated && tracked.Customer != null && tracked.Customer.HumanType == HumanType.Bad)
                {
                    Raise(badMissed, ReviewType.Bad);
                }
            }

            Unhook(session, hooks);
            _sessions.Remove(session);
        }

        private static void Unhook(VisitSession session, SessionHooks hooks)
        {
            session.Fueled -= hooks.OnFueled;
            session.FuelLate -= hooks.OnFuelLate;
            session.Completed -= hooks.OnCompleted;

            for (int i = 0; i < hooks.Customers.Count; i++)
            {
                Untrack(hooks.Customers[i]);
            }
        }

        private static void Untrack(Tracked tracked)
        {
            if (tracked.Health == null)
            {
                return;
            }

            tracked.Health.Damaged -= tracked.OnDamaged;
            tracked.Health.Died -= tracked.OnDied;
            tracked.Health = null;
        }

        private static bool IsFromCustomer(HitInfo hit)
        {
            return hit.Attacker != null && hit.Attacker.GetComponentInParent<AbstractCustomer>() != null;
        }

        private void Raise(int amount, ReviewType type)
        {
            if (amount == 0 || reviewChannel == null)
            {
                return;
            }

            reviewChannel.RaiseEvent(UIEvents.ReviewEvent.IncreaseValue(amount, type));
        }
    }
}
