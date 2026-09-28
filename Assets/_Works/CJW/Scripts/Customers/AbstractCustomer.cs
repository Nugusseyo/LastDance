using _Works.CJW.Scripts.Customers.Ragdoll;
using System;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Animation;
using _Works.CJW.Scripts.Customers.Appearance;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.Customers.Health;
using _Works.CJW.Scripts.Customers.Interaction;
using _Works.CJW.Scripts.Customers.Movement;
using _Works.CJW.Scripts.Customers.Patience;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.CJW.Scripts.Sounds;
using _Works.Shared.Boarding;
using _Works.Shared.Combat;
using DevLib.ObjectPool.Runtime;
using Resources.DataBase.Human_Data;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers
{
    /// <summary>손님의 데이터와 모듈만 소유한다. 걷기는 <see cref="IMover"/>가, 탑승은 <see cref="_Works.Shared.Boarding.IBoardable"/> 모듈이 맡는다.
    /// 모듈은 자식 오브젝트에 하나씩 붙어 있다.</summary>
    public abstract class AbstractCustomer : ManagingAgent, IPoolable, IHittable
    {
        [field: SerializeField] public HumanType HumanType { get; private set; } = HumanType.Good;

        /// <summary>평판 리뷰 글 번호(ReviewDB index) 후보. HumanDB 대사 번호와 맞춘다(기획서 표).
        /// 여럿이면 말풍선과 같은 갈래(CustomerContext.PickVariant)로 골라, 한 대사와 리뷰 글이 짝이 맞는다. 비우면 손님이 말하는 대사 번호로 고른다.</summary>
        [field: SerializeField, Tooltip("평판 리뷰 글 번호(ReviewDB index) 후보. HumanDB 대사 번호와 맞춘다. 여럿이면 말풍선과 같은 갈래로 고른다. 비우면 대사 번호로 고른다.")]
        public int[] ReviewIndices { get; private set; }

        [field: SerializeField] public NavMeshAgent Agent { get; private set; }
        [field: SerializeField] public PoolItemSO PoolItem { get; set; }
        
        public IBoardable Boarding { get; private set; }

        /// <summary>걷기. 프리팹에 이동 모듈이 없으면 null이다.</summary>
        public IMover Mover { get; private set; }

        /// <summary>춤·주먹질 같은 연출 재생. 프리팹에 연출 모듈이 없으면 null이다.</summary>
        public IActionAnimator ActionAnimator { get; private set; }

        /// <summary>걸음걸이 변조. 이상하게 걷는 손님에게만 있고, 없으면 null이다.</summary>
        public IGait Gait { get; private set; }

        /// <summary>플레이어에게 무언가를 요구하는 창구. 요구하지 않는 손님에게는 없으므로 null이다.</summary>
        public ICustomerRequest Request { get; private set; }

        /// <summary>물리로 쓰러지는 몸. 프리팹에 래그돌 모듈이 없으면 null이다.</summary>
        public IRagdoll Ragdoll { get; private set; }

        /// <summary>체력과 맞는 창구. 프리팹에 체력 모듈이 없으면 null이고, 그 손님은 맞지 않는다.</summary>
        public ICustomerHealth Health { get; private set; }

        /// <summary>맞은 순간의 손맛 연출. 프리팹에 연출 모듈이 없으면 null이고, 그 손님은 움찔만 하고 번쩍이거나 피를 흘리지 않는다.</summary>
        public ICustomerHitFeedback HitFeedback { get; private set; }

        /// <summary>플레이어를 기다리며 줄어드는 인내심. 프리팹에 인내심 모듈이 없으면 null이고, 그 손님은 참을성을 드러내지 않는다.</summary>
        public ICustomerPatience Patience { get; private set; }

        /// <summary>소리를 내는 창구. 프리팹에 사운드 모듈이 없으면 null이고, 그 손님은 소리 없이 움직인다.</summary>
        public ISoundEmitter Sound { get; private set; }

        /// <summary>겉모습. 스폰될 때마다 무작위로 갈아입는다. 프리팹에 겉모습 모듈이 없으면 null이고, 그 손님은 프리팹 모습 그대로 나온다.</summary>
        public ICustomerAppearance Appearance { get; private set; }

        /// <summary>때리는 쪽은 맞은 콜라이더에서 GetComponentInParent로 <see cref="IHittable"/>을 찾는데, 체력 모듈은 자식에 있어
        /// 루트 콜라이더에서는 닿지 않는다. 루트가 받아 체력 모듈에 넘긴다.</summary>
        public bool CanBeHit => Health != null && Health.CanBeHit;

        public void TakeHit(HitInfo hit)
        {
            Health?.TakeHit(hit);
        }

        /// <summary>쓰러져 있는지. 이 동안 손님은 아무 행동도 시작하지 않는다.</summary>
        public bool IsKnockedDown => Ragdoll != null && Ragdoll.IsActive;
        public GameObject GameObject => this != null ? gameObject : null;
        /// <summary>이 손님이 참여 중인 방문. 방문 밖에서는 null이다.</summary>
        public VisitSession Session { get; private set; }

        /// <summary>세션이 바뀔 때 알린다. 모듈이 전역 이벤트 대신 이걸 구독한다.</summary>
        public event Action<VisitSession> SessionChanged;

        /// <summary>행동 머신. 프리팹에 CustomerFSMModule이 없으면 null이다.</summary>
        public CustomerFSMModule Fsm { get; private set; }
        /// <summary>세션이 손님 목록을 순회하며 직접 물려준다. 전역 방송을 쓰면 다른 차의 세션에 붙을 수 있어 직접 호출한다.</summary>
        public void BindSession(VisitSession session)
        {
            Session = session;
            SessionChanged?.Invoke(session);
        }
        

        /// <summary>이 손님의 수치. 스폰될 때 <see cref="Setup"/>으로 주입된다.</summary>
        public CustomerDataSO Data { get; set; }

        /// <summary>풀에서 꺼낸 직후 이 손님이 쓸 데이터를 넣어준다.</summary>
        public virtual void Setup(CustomerDataSO data)
        {
            Data = data;
            Appearance?.Randomize();

            if (data == null || Agent == null)
            {
                return;
            }

            if (data.MoveSpeed > 0f)
            {
                Agent.speed = data.MoveSpeed;
            }

            if (data.AngularSpeed > 0f)
            {
                Agent.angularSpeed = data.AngularSpeed;
            }

            if (data.StoppingDistance >= 0f)
            {
                Agent.stoppingDistance = data.StoppingDistance;
            }
        }

        public virtual void ResetItem()
        {
            // Stop()을 빼먹으면 대기 중이던 상태가 좀비로 남아 계속 돌고,
            // Context.Reset()을 빼먹으면 이전 방문의 Target이 다음 손님에게 샌다.
            Fsm?.Stop();
            Fsm?.Context?.Reset();

            BindSession(null);

            // 다음 스폰에서 Setup이 다시 넣어주니까 비워놔야한다.
            Data = null;
            Boarding?.ResetBoarding();

            if (Agent != null)
            {
                Agent.enabled = true;
                if (Agent.isOnNavMesh)
                {
                    Agent.ResetPath();
                }
            }

            // 쓰러진 채 반납됐을 수 있다. 일으키지 않으면 다음 손님이 누운 채로, 길찾기도 꺼진 채로 나온다.
            if (IsKnockedDown)
            {
                Ragdoll.Recover();
            }

            // 걷던 중에 반납됐을 수 있다. 여기서 접지 않으면 다음 손님이 선 자리에서 걷는 애니메이션으로 시작한다.
            Mover?.Stop();

            // 춤추다 반납됐을 수 있다. 접지 않으면 다음 손님이 풀에서 나오자마자 춤부터 춘다.
            ActionAnimator?.End();

            // 답을 기다리던 요구도 닫는다. 빼먹으면 UI가 사라진 손님의 요구를 계속 띄운다.
            if (Request != null && Request.IsPending)
            {
                Request.Withdraw();
            }

            // 맞던 손님이 반납됐을 수 있다. 채우지 않으면 다음 손님이 깎인 체력으로 나온다.
            Health?.ResetHealth();
            HitFeedback?.Cancel();

            // 기다리다 반납됐을 수 있다. 끄지 않으면 다음 손님이 줄어든 인내심을 띄운 채로 나온다.
            Patience?.End();
        }
    

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            
            Boarding = GetModule<IBoardable>();
            Fsm = GetModule<CustomerFSMModule>();
            Mover = GetModule<IMover>();
            ActionAnimator = GetModule<IActionAnimator>();
            Gait = GetModule<IGait>();
            Request = GetModule<ICustomerRequest>();
            Ragdoll = GetModule<IRagdoll>();
            Health = GetModule<ICustomerHealth>();
            HitFeedback = GetModule<ICustomerHitFeedback>();
            Patience = GetModule<ICustomerPatience>();
            Sound = GetModule<ISoundEmitter>();
            Appearance = GetModule<ICustomerAppearance>();
        }
}
}
