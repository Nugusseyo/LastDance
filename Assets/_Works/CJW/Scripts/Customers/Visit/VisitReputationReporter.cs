using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Customers.Data;
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
    /// 주유 결과는 방문(<see cref="VisitSession.Fueled"/>/<see cref="VisitSession.FuelLate"/>)에서, 퇴치·폭행은 손님 체력 이벤트에서 듣는다.
    /// 일반인·진상 판정은 손님의 대사 index를 HumanDB에서 찾은 type으로 한다(<see cref="EvaluatedType"/>).</summary>
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

        // 평판이 얼마나 오르내리는지는 ReviewManager가 리뷰 종류(Good/Bad/Late)로 정한다. 여기서는 무엇을 알릴지만 고른다.
        // 이벤트의 숫자는 변화량이 아니라 ReviewDB의 리뷰 글 index다 — 없는 index(음수 등)를 보내면 리뷰 UI가 예외를 던진다.
        [Header("알릴 일")]
        [Tooltip("주유를 해 줬을 때 (좋은 리뷰).")]
        [SerializeField] private bool reportFueled = true;
        [Tooltip("주유가 너무 늦어졌을 때 (늦음 리뷰).")]
        [SerializeField] private bool reportFuelLate = true;
        [Tooltip("진상(HumanDB type Bad)을 퇴치했을 때 (좋은 리뷰).")]
        [SerializeField] private bool reportBadDefeated = true;
        [Tooltip("진상을 퇴치하지 못하고 방문이 끝났을 때 (나쁜 리뷰).")]
        [SerializeField] private bool reportBadMissed = true;
        [Tooltip("일반인(HumanDB type Good)을 때렸을 때, 한 대마다 (나쁜 리뷰).")]
        [SerializeField] private bool reportGoodHit = true;

        [Tooltip("손님이 아무 대사도 하지 않았을 때 쓸 리뷰 글 index(ReviewDB).")]
        [SerializeField, Min(1)] private int fallbackReviewIndex = 1;

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
            hooks.OnFueled = customer => Raise(reportFueled, ReviewType.Good, customer, "주유 받음");
            hooks.OnFuelLate = customer => Raise(reportFuelLate, ReviewType.Late, customer, "주유 늦음");
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
            if ((EvaluatedType(tracked.Customer) == HumanType.Good || IsHarmless(tracked.Customer)) && !IsFromCustomer(hit))
            {
                Raise(reportGoodHit, ReviewType.Bad, tracked.Customer, IsHarmless(tracked.Customer) ? "돌아다니던 진상(일반인 취급)을 때림" : "일반인을 때림");
            }
        }

        private void OnDied(Tracked tracked)
        {
            if (ActsBad(tracked.Customer))
            {
                Raise(reportBadDefeated, ReviewType.Good, tracked.Customer, "진상 퇴치");
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
                if (!tracked.Defeated && tracked.Customer != null && ActsBad(tracked.Customer))
                {
                    Raise(reportBadMissed, ReviewType.Bad, tracked.Customer, "진상을 놓침");
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

        /// <summary>평판에서 진상으로 셈할지. 진상(HumanDB type Bad)이라도 조건이 안 맞아 진상 짓을 못 하고 돌아다니는 손님
        /// (<see cref="CustomerFSM.CustomerContext.Harmless"/>)은 일반인으로 본다 — 때리면 깎이고, 퇴치해도 오르지 않고, 그냥 떠나도 깎이지 않는다.</summary>
        private bool ActsBad(AbstractCustomer customer)
        {
            return EvaluatedType(customer) == HumanType.Bad && !IsHarmless(customer);
        }

        /// <summary>이 손님을 일반인으로 볼지 진상으로 볼지. 리뷰 글과 같은 index(기획서 표의 INDEX)를 HumanDB에서 찾아 그 type을 쓴다.
        /// 후보 index가 모두 같은 type이면 갈래를 고르지 않는다 — 말하기 전에 맞았다고 갈래(<see cref="CustomerFSM.CustomerContext.PickVariant"/>)가
        /// 먼저 정해지면 싸움꾼 짝이 같은 대사를 고를 수 있다. index가 HumanDB에 없거나 대사가 없는 손님이면 프리팹의 HumanType을 쓴다.</summary>
        private HumanType EvaluatedType(AbstractCustomer customer)
        {
            if (customer == null)
            {
                return HumanType.None;
            }

            CustomerFSM.CustomerFSMModule fsm = customer.Fsm;
            CustomerFSM.CustomerContext ctx = fsm != null ? fsm.Context : null;
            int[] candidates = customer.ReviewIndices is { Length: > 0 } reviews ? reviews : fsm != null ? fsm.ConfiguredLines() : null;
            bool noCandidates = candidates == null || candidates.Length == 0;

            // 이미 한 대사가 후보에 있으면 리뷰 글도 그 번호다(ReviewIndexOf와 같은 규칙).
            if (ctx != null && ctx.LineIndex > 0 && (noCandidates || Array.IndexOf(candidates, ctx.LineIndex) >= 0))
            {
                return HumanTypeTable.Of(ctx.LineIndex, customer.HumanType);
            }

            if (noCandidates)
            {
                return customer.HumanType;
            }

            HumanType first = HumanTypeTable.Of(candidates[0], customer.HumanType);
            for (int i = 1; i < candidates.Length; i++)
            {
                if (HumanTypeTable.Of(candidates[i], customer.HumanType) != first)
                {
                    return HumanTypeTable.Of(ReviewIndexOf(customer, out _), customer.HumanType);
                }
            }

            return first;
        }

        private static bool IsHarmless(AbstractCustomer customer)
        {
            return customer.Fsm?.Context?.Harmless == true;
        }

        private static bool IsFromCustomer(HitInfo hit)
        {
            return hit.Attacker != null && hit.Attacker.GetComponentInParent<AbstractCustomer>() != null;
        }

        private void Raise(bool report, ReviewType type, AbstractCustomer customer, string reason)
        {
            if (!report || reviewChannel == null)
            {
                return;
            }

            int index = ReviewIndexOf(customer, out string source);

            // 어떤 손님 때문에 어떤 글이 나갔는지 남긴다. 리뷰 알림만 봐서는 누가 왜 올리고 깎았는지 알 수 없다.
            Debug.Log($"[Reputation] {(customer != null ? customer.name : "?")}({EvaluatedType(customer)}) {reason} → {type} 리뷰 {index}번({source})",
                customer);

            reviewChannel.RaiseEvent(UIEvents.ReviewEvent.Review(index, type));
        }

        /// <summary>리뷰 글은 손님 대사와 같은 index를 쓴다(HumanDB와 ReviewDB가 번호를 맞춰 둠).
        /// 말한 대사가 있으면 그 번호, 아직 말하기 전이면(싸움꾼은 마주 설 때, 차 때리는 손님은 차를 찾았을 때 말한다) 그 손님에게 설정된 대사 중
        /// 이번 방문의 갈래로 고른 번호, 대사가 아예 없는 손님만 기본 글을 쓴다. 전에는 말하기 전이면 늘 기본 글(주유 손님 글)이 나갔다.
        /// 손님에 리뷰 글 번호(<see cref="AbstractCustomer.ReviewIndices"/>)를 정해 두었으면 그게 먼저다 — 이미 한 대사가 후보에 있으면 그 번호,
        /// 아니면 말풍선과 같은 갈래로 고른다.</summary>
        private int ReviewIndexOf(AbstractCustomer customer, out string source)
        {
            CustomerFSM.CustomerFSMModule fsm = customer != null ? customer.Fsm : null;
            CustomerFSM.CustomerContext ctx = fsm != null ? fsm.Context : null;

            int[] reviews = customer != null ? customer.ReviewIndices : null;
            if (reviews != null && reviews.Length > 0)
            {
                source = "손님에 정한 글";
                if (ctx != null && Array.IndexOf(reviews, ctx.LineIndex) >= 0)
                {
                    return ctx.LineIndex;
                }

                return ctx != null ? reviews[ctx.PickVariant(reviews.Length)] : reviews[0];
            }

            if (ctx != null && ctx.LineIndex > 0)
            {
                source = "한 대사";
                return ctx.LineIndex;
            }

            int[] lines = fsm != null ? fsm.ConfiguredLines() : null;
            if (lines != null && lines.Length > 0)
            {
                source = "설정된 대사";
                return ctx != null ? lines[ctx.PickVariant(lines.Length)] : lines[0];
            }

            source = "대사 없음, 기본 글";
            return fallbackReviewIndex;
        }
    }
}
