using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM
{
    /// <summary>
    /// 손님 한 명의 행동 머신. MonoBehaviour가 아니라 순수 C#이고,
    /// 직렬화와 수명은 <see cref="CustomerFSMModule"/>이 맡는다.
    ///
    /// 세션이 Phase를 넘길 때 <see cref="RunPhase"/>를 부르고, 그 Phase의 시퀀스가 끝나면
    /// UniTask가 완료된다. 이것이 세션과 손님을 잇는 유일한 접점이다 —
    /// 머신은 자기가 어느 Phase에 있는지 스스로 판단하지 않는다.
    ///
    /// 전이 통로는 둘이다.
    ///  - 정상 완료: 상태가 <see cref="VisitOutcome"/>을 반환하고 다음 인덱스로
    ///  - 인터럽트: 토큰이 취소되고 지정된 상태로 갈아탄 뒤 원래 상태부터 재개
    /// </summary>
    public sealed class CustomerStateMachine
    {
        private readonly CustomerContext _ctx;
        private readonly Dictionary<VisitPhase, CustomerState[]> _sequences = new();
        private readonly List<CustomerState> _all = new();

        /// <summary>손님이 풀에서 나와 있는 동안의 수명. 반납 시 취소된다.</summary>
        private CancellationTokenSource _lifetime;

        /// <summary>지금 돌고 있는 Phase 시퀀스의 수명. 다음 Phase가 시작되면 취소된다.</summary>
        private CancellationTokenSource _phase;

        /// <summary>지금 돌고 있는 상태 하나의 수명. 인터럽트가 이걸 취소한다.</summary>
        private CancellationTokenSource _running;

        private CustomerState _interruptTarget;

        /// <summary>
        /// 전이 세대. 인터럽트를 걸었던 그 상태가 아직 돌고 있을 때만 유효하도록 묶는다.
        /// 이게 없으면 상태가 정상 완료되는 순간 들어온 인터럽트가 다음 상태를 엉뚱하게 바꾼다.
        /// </summary>
        private int _generation;
        private int _interruptGeneration = -1;

        public CustomerContext Context => _ctx;

        /// <summary>지금 실행 중인 상태. 디버깅용.</summary>
        public CustomerState Current { get; private set; }

        public bool IsRunning => _lifetime != null;

        public CustomerStateMachine(CustomerContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>모듈이 직렬화한 Phase 시퀀스를 등록한다.</summary>
        public void Register(VisitPhase phase, CustomerState[] states)
        {
            if (states == null || states.Length == 0)
            {
                return;
            }

            _sequences[phase] = states;

            for (int i = 0; i < states.Length; i++)
            {
                Bind(states[i]);
            }
        }

        /// <summary>시퀀스에 없는 상태(인터럽트 대상 등)도 컨텍스트를 물려준다.</summary>
        public void Bind(CustomerState state)
        {
            if (state == null || _all.Contains(state))
            {
                return;
            }

            state.Bind(_ctx);
            _all.Add(state);
        }

        /// <summary>방문 시작. 이전 수명이 남아 있으면 정리하고 새로 연다.</summary>
        public void Begin(VisitContext visit, int seatIndex)
        {
            Stop();

            for (int i = 0; i < _all.Count; i++)
            {
                _all[i].Reset();
            }

            _ctx.SetVisit(visit, seatIndex);
            _lifetime = new CancellationTokenSource();
        }

        /// <summary>
        /// 방문 종료. 풀 반납 시 반드시 호출해야 한다.
        /// 호출하지 않으면 대기 중이던 상태가 좀비로 남는다.
        /// </summary>
        public void Stop()
        {
            _phase?.Cancel();
            _running?.Cancel();

            if (_lifetime != null)
            {
                _lifetime.Cancel();
                _lifetime.Dispose();
                _lifetime = null;
            }

            _interruptTarget = null;
            _interruptGeneration = -1;
            Current = null;
        }

        /// <summary>
        /// 해당 Phase의 시퀀스를 순서대로 실행하고, 전부 끝나면 완료된다.
        /// 등록된 상태가 없으면 즉시 완료된다 — 차에서 안 내리는 손님이
        /// Unloading 칸을 비워두는 방식이 이것이다.
        /// </summary>
        public async UniTask RunPhase(VisitPhase phase)
        {
            if (_lifetime == null)
            {
                return;
            }

            // 이전 Phase 시퀀스가 아직 돌고 있으면 끊는다.
            // 안 그러면 Waiting에서 무한 대기하던 상태가 Boarding 중에도 계속 돈다.
            _phase?.Cancel();

            if (!_sequences.TryGetValue(phase, out CustomerState[] sequence))
            {
                return;
            }

            CancellationTokenSource phaseCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _phase = phaseCts;
            CancellationToken outer = phaseCts.Token;

            try
            {
                int index = 0;

                while (index < sequence.Length && !outer.IsCancellationRequested)
                {
                    CustomerState state = sequence[index];
                    if (state == null)
                    {
                        index++;
                        continue;
                    }

                    RunResult result = await RunOne(state, outer);

                    if (outer.IsCancellationRequested)
                    {
                        return;
                    }

                    if (result.Interrupted)
                    {
                        CustomerState target = _interruptTarget;
                        _interruptTarget = null;

                        if (target != null)
                        {
                            // 인터럽트 상태를 끝까지 돌린 뒤 원래 상태부터 재개한다.
                            // (인터럽트 중의 재인터럽트는 지금은 무시한다)
                            await RunOne(target, outer);
                        }

                        continue;
                    }

                    if (result.Outcome == VisitOutcome.Failed)
                    {
                        return;
                    }

                    index++;
                }
            }
            finally
            {
                if (ReferenceEquals(_phase, phaseCts))
                {
                    _phase = null;
                }

                phaseCts.Dispose();
            }
        }

        /// <summary>
        /// 어느 상태에서든 지정한 상태로 갈아탄다.
        /// 상태마다 "전투로 갈 수 있는가"를 정의할 필요가 없어 상태 곱셈이 막힌다.
        /// </summary>
        public void Interrupt(CustomerState to)
        {
            if (to == null || _running == null || _running.IsCancellationRequested)
            {
                return;
            }

            Bind(to);

            _interruptTarget = to;
            _interruptGeneration = _generation;
            _running.Cancel();
        }

        private async UniTask<RunResult> RunOne(CustomerState state, CancellationToken outer)
        {
            _generation++;
            int generation = _generation;

            Current = state;
            CancellationTokenSource runningCts = CancellationTokenSource.CreateLinkedTokenSource(outer);
            _running = runningCts;

            try
            {
                VisitOutcome outcome = await state.Run(runningCts.Token);
                return new RunResult(false, outcome);
            }
            catch (OperationCanceledException)
            {
                bool byInterrupt = !outer.IsCancellationRequested && _interruptGeneration == generation;
                return new RunResult(byInterrupt, VisitOutcome.Failed);
            }
            catch (Exception e)
            {
                // 흘려보내면 손님 하나가 조용히 멈춘다. 여기서 반드시 남긴다.
                Debug.LogException(e, _ctx.Customer);
                return new RunResult(false, VisitOutcome.Failed);
            }
            finally
            {
                if (ReferenceEquals(_running, runningCts))
                {
                    _running = null;
                }

                runningCts.Dispose();
                Current = null;
            }
        }

        private readonly struct RunResult
        {
            public readonly bool Interrupted;
            public readonly VisitOutcome Outcome;

            public RunResult(bool interrupted, VisitOutcome outcome)
            {
                Interrupted = interrupted;
                Outcome = outcome;
            }
        }
    }
}
