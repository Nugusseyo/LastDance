using System;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.MapSystems;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>
    /// 손님 상태 머신의 껍데기. 직렬화와 수명만 맡고 로직은 <see cref="CustomerStateMachine"/>에 있다.
    ///
    /// 손님 타입은 이 모듈에 꽂힌 시퀀스가 정의한다. 상태는 [SerializeReference]로
    /// 프리팹에 직접 저장되므로 설정값이 그 손님 타입에만 속한다 —
    /// 같은 행동을 손님마다 다른 값으로 쓸 수 있고, 공유 에셋을 건드릴 일이 없다.
    /// </summary>
    public class CustomerFSMModule : AbstractModule
    {
        [Serializable]
        private class PhaseSequence
        {
            [Tooltip("이 시퀀스가 도는 방문 단계.")]
            public VisitPhase Phase;

            [Tooltip("위에서부터 순서대로 실행된다. 비워두면 이 단계에 할 일이 없다는 뜻.")]
            [SerializeReference] public CustomerState[] States;
        }

        [Header("방문 시퀀스")]
        [Tooltip("Phase마다 이 손님이 할 행동. 등록되지 않은 Phase는 즉시 넘어간다.")]
        [SerializeField] private PhaseSequence[] sequences;

        [Header("공용 참조")]
        [Tooltip("목적지를 물어볼 맵 데이터. 상태마다 따로 꽂지 않도록 여기 하나만 둔다.")]
        [SerializeField] private MapDataSo mapData;

        [Header("인터럽트 대상")]
        [Tooltip("피격 등으로 전투에 들어갈 때 갈아탈 상태. 전투하지 않는 손님은 비워둔다.")]
        [SerializeReference] private CustomerState combat;

        [Tooltip("도망칠 때 갈아탈 상태.")]
        [SerializeReference] private CustomerState flee;

        private AbstractCustomer _customer;

        public CustomerStateMachine Machine { get; private set; }

        public CustomerContext Context => Machine?.Context;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _customer = owner as AbstractCustomer;
            if (_customer == null)
            {
                Debug.LogError("[CustomerFSMModule] AbstractCustomer를 상속한 오브젝트에 붙여야 합니다.", this);
                return;
            }

            CustomerContext context = new CustomerContext();
            Machine = new CustomerStateMachine(context);
            context.Bind(_customer, Machine, mapData);

            if (sequences != null)
            {
                for (int i = 0; i < sequences.Length; i++)
                {
                    Machine.Register(sequences[i].Phase, sequences[i].States);
                }
            }

            // 시퀀스에 없어도 인터럽트로 진입할 수 있으므로 컨텍스트를 미리 물려준다.
            Machine.Bind(combat);
            Machine.Bind(flee);
        }

        /// <summary>방문 시작. VisitSession.Begin이 손님마다 호출한다.</summary>
        /// <summary>방문 시작. VisitSession.Begin이 손님마다 호출한다.</summary>
        public void Begin(VisitContext visit, int seatIndex)
        {
            Machine?.Begin(visit, seatIndex);
        }

        /// <summary>방문 종료. 풀 반납 전에 반드시 호출한다.</summary>
        public void Stop()
        {
            Machine?.Stop();
        }

        /// <summary>해당 단계의 시퀀스를 돌린다. 세션이 Phase를 넘길 때 부른다.</summary>
        public UniTask RunPhase(VisitPhase phase)
        {
            return Machine != null ? Machine.RunPhase(phase) : UniTask.CompletedTask;
        }

        /// <summary>전투 진입. 대상을 컨텍스트에 넣고 인터럽트를 건다.</summary>
        public void EnterCombat(Transform target)
        {
            if (Machine == null || combat == null)
            {
                return;
            }

            Machine.Context.Target = target;
            Machine.Interrupt(combat);
        }

        /// <summary>도망. 어느 상태에서든 호출할 수 있다.</summary>
        public void RunAway()
        {
            if (Machine == null || flee == null)
            {
                return;
            }

            Machine.Interrupt(flee);
        }
    }
}
