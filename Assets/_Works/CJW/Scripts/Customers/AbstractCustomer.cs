using System;
using _Works.CJW.Scripts.Customers.Visit;
using _Works.CJW.Scripts.Customers.Visit.CustomerFSM;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.Shared.Boarding;
using DevLib.ObjectPool.Runtime;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Customers
{
    /// <summary>
    /// 손님의 데이터와 이동만 소유한다.
    /// 탑승은 <see cref="_Works.Shared.Boarding.IBoardable"/> 모듈이 맡는다.
    /// </summary>
    public abstract class AbstractCustomer : ManagingAgent, IPoolable
    {
        [field: SerializeField] public NavMeshAgent Agent { get; private set; }
        [field: SerializeField] public PoolItemSO PoolItem { get; set; }
        
        public IBoardable Boarding { get; private set; }
        public GameObject GameObject => this != null ? gameObject : null;
        /// <summary>이 손님이 참여 중인 방문. 방문 밖에서는 null이다.</summary>
        public VisitSession Session { get; private set; }

        /// <summary>세션이 바뀔 때 알린다. 모듈이 전역 이벤트 대신 이걸 구독한다.</summary>
        public event Action<VisitSession> SessionChanged;

        /// <summary>행동 머신. 프리팹에 CustomerFSMModule이 없으면 null이다.</summary>
        public CustomerFSMModule Fsm { get; private set; }
        /// <summary>
        /// 세션이 손님 목록을 순회하며 직접 물려준다.
        /// 전역 방송을 쓰면 수신자마다 "내 세션인가" 필터가 필요하고, 그 필터가 빠지면
        /// 다른 차의 세션에 붙는다. 세션은 자기 손님이 누군지 이미 알고 있으므로 방송할 이유가 없다.
        /// </summary>
        public void BindSession(VisitSession session)
        {
            Session = session;
            SessionChanged?.Invoke(session);
        }
        

        /// <summary>이 손님의 수치. 스폰될 때 <see cref="Setup"/>으로 주입된다.</summary>
        public CustomerDataSO Data { get; private set; }

        /// <summary>
        /// 탑승 중에는 탑승 모듈이 Agent를 꺼두므로 Agent.enabled 하나로 걸러진다.
        /// 손님이 탑승 여부를 따로 들고 있지 않아도 되는 이유다.
        /// </summary>
        public bool IsArrived =>
            Agent != null &&
            Agent.enabled &&
            !Agent.pathPending &&
            Agent.remainingDistance <= Agent.stoppingDistance;

        /// <summary>풀에서 꺼낸 직후 이 손님이 쓸 데이터를 넣어준다.</summary>
        public virtual void Setup(CustomerDataSO data)
        {
            Data = data;
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

        public void MoveTo(Vector3 destination)
        {
            // 탑승 중이면 탑승 모듈이 Agent를 꺼둔 상태라 여기서 자연히 걸러진다.
            if (Agent == null || !Agent.enabled || !Agent.isOnNavMesh)
            {
                return;
            }

            Agent.SetDestination(destination);
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
        }
    

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            
            Boarding = GetModule<IBoardable>();
            Fsm = GetModule<CustomerFSMModule>();
        }
}
}
