using _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using _Works.CJW.Scripts.Customers.Animation;
using _Works.CJW.Scripts.MapSystems;
using DevLib.AnimatorSystem;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>손님 상태 머신의 껍데기. 직렬화와 수명만 맡고 로직은 <see cref="CustomerStateMachine"/>에 있다. 상태는 [SerializeReference]로 프리팹에 직접 저장되어 손님마다 다른 값을 쓸 수 있다.</summary>
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

        /// <summary>전투 상태가 꽂혀 있어 <see cref="EnterCombat"/>이 실제로 무언가를 하는지.</summary>
        public bool CanFight => combat != null;

        private AbstractCustomer _customer;

        public CustomerStateMachine Machine { get; private set; }

        public CustomerContext Context => Machine?.Context;

        /// <summary>이 손님이 방문 중에 말하게 될 대사 index 후보. 시퀀스를 단계 순서대로 훑어 처음으로 대사를 가진 행동의 것을 돌려준다.
        /// 리뷰 글을 고를 때 손님이 아직 말하기 전이어도(말하는 시점이 뒤인 싸움꾼·차 때리는 손님 등) 그 손님다운 글을 쓰게 한다. 없으면 null.</summary>
        public int[] ConfiguredLines()
        {
            if (sequences == null)
            {
                return null;
            }

            for (int i = 0; i < sequences.Length; i++)
            {
                CustomerState[] states = sequences[i]?.States;
                if (states == null)
                {
                    continue;
                }

                for (int j = 0; j < states.Length; j++)
                {
                    if (states[j] is ISpeakingState speaking && speaking.SpokenLines != null && speaking.SpokenLines.Length > 0)
                    {
                        return speaking.SpokenLines;
                    }
                }
            }

            return null;
        }

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
                // 같은 Phase가 두 번 등록되면 Register가 Dictionary에 덮어써 앞의 시퀀스가 통째로 사라진다.
                HashSet<VisitPhase> seen = new HashSet<VisitPhase>();

                for (int i = 0; i < sequences.Length; i++)
                {
                    VisitPhase phase = sequences[i].Phase;

                    // VisitPhase는 4번이 비어 있는 enum이다. 정의되지 않은 값에 등록된 시퀀스는
                    // RunPhase의 TryGetValue가 영영 찾지 못해 조용히 통째로 건너뛰어진다.
                    if (!Enum.IsDefined(typeof(VisitPhase), phase))
                    {
                        Debug.LogError($"[CustomerFSMModule] 정의되지 않은 Phase({(int)phase})에 시퀀스가 등록되어 있어 실행되지 않습니다.", this);
                        continue;
                    }

                    if (!seen.Add(phase))
                    {
                        Debug.LogError($"[CustomerFSMModule] {phase} 시퀀스가 두 번 등록되어 앞의 것이 무시됩니다.", this);
                    }

                    Machine.Register(phase, sequences[i].States);
                }
            }

            // 시퀀스에 없어도 인터럽트로 진입할 수 있으므로 컨텍스트를 미리 물려준다.
            Machine.Bind(combat);
            Machine.Bind(flee);
        }

        /// <summary>시퀀스 어딘가에서 주유를 원하는지. 초기화 전에도 직렬화된 값만 보므로 프리팹 에셋에 바로 물어볼 수 있다.</summary>
        public bool WantsFuel => AnyState(state => state.WantsFuel);

        /// <summary>시퀀스 어딘가의 행동이 진상 짓을 시작하는 순간을 스스로 알리는지. 평판은 그 순간에 깎고 방문 끝에 다시 깎지 않는다.</summary>
        public bool ReportsMisconduct => AnyState(state => state.ReportsMisconduct);

        private bool AnyState(Predicate<CustomerState> match)
        {
            if (sequences == null)
            {
                return false;
            }

            for (int i = 0; i < sequences.Length; i++)
            {
                CustomerState[] states = sequences[i]?.States;
                if (states == null)
                {
                    continue;
                }

                for (int j = 0; j < states.Length; j++)
                {
                    if (states[j] != null && match(states[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

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

        /// <summary>쓰러짐. 하던 행동을 끊고 일어설 때까지 기다린 뒤 그 행동을 처음부터 다시 한다.
        /// 쓰러짐은 어느 손님이든 같아서 프리팹에 두지 않고 코드로 하나 만든다.</summary>
        public void KnockDown()
        {
            if (Machine == null)
            {
                return;
            }

            if (_knockedDown == null)
            {
                _knockedDown = new KnockedDownState();
                Machine.Bind(_knockedDown);
            }

            Machine.Interrupt(_knockedDown);
        }

        private KnockedDownState _knockedDown;

        private HitReactState _hitReact;

        /// <summary>맞았다. 하던 걸 끊고 <paramref name="hitFrom"/>(때린 쪽 위치)으로 돌아서며 <paramref name="clip"/>으로 <paramref name="duration"/>초 움찔한 뒤,
        /// <paramref name="fightBack"/>이면 때린 쪽에게 덤비고(전투 상태가 없으면 도망) 아니면 하던 행동을 처음부터 다시 한다.
        /// 움찔하는 도중에 또 맞으면 처음부터 다시 움찔한다.</summary>
        public void TakeHit(HashDataSO clip, float duration, Vector3 hitFrom, Transform attacker, bool fightBack)
        {
            if (Machine == null)
            {
                return;
            }

            if (_hitReact == null)
            {
                _hitReact = new HitReactState();
                Machine.Bind(_hitReact);
            }

            CustomerState then = null;
            if (fightBack)
            {
                if (attacker != null && combat != null)
                {
                    Machine.Context.Target = attacker;
                    then = combat;
                }
                else
                {
                    then = flee;
                }
            }

            _hitReact.Prepare(clip, duration, hitFrom, then);

            // 이미 움찔하는 중이면 Prepare가 다시 시작시킨다. 인터럽트를 또 걸면 움찔하던 걸 끊고 원래 행동으로 돌아가 버린다.
            if (ReferenceEquals(Machine.Current, _hitReact))
            {
                return;
            }

            if (Machine.CanInterrupt)
            {
                Machine.Interrupt(_hitReact);
                return;
            }

            // 돌고 있는 행동이 없다(행동 사이·시퀀스가 끝난 뒤). 끊을 게 없으니 때린 쪽으로 바로 돌아서고 클립만 잠깐 튼다.
            if (_customer != null)
            {
                HitReactState.TurnTowards(_customer.transform, hitFrom, 360f);
            }

            IActionAnimator action = _customer != null ? _customer.ActionAnimator : null;
            if (action != null && clip != null)
            {
                // 클립을 먼저 튼다(PlayFor는 첫 대기 전에 클립을 건다). 그래야 이동 모듈이 서기 클립으로 덮지 않는다.
                action.PlayFor(clip, duration, destroyCancellationToken).Forget();
                _customer.Mover?.Stop();
            }
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
