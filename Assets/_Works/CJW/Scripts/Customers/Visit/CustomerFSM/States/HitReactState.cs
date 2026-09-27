using System;
using System.Threading;
using _Works.CJW.Scripts.Customers.Animation;
using Cysharp.Threading.Tasks;
using DevLib.AnimatorSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.CustomerFSM.States
{
    /// <summary>맞아서 움찔한다. 하던 걸 멈추고 피격 클립을 잠깐 튼 뒤, 정해 둔 반응(반격·도망)이 있으면 이어서 하고,
    /// 없으면 끝나 원래 하던 행동으로 돌아간다. 인터럽트로만 들어온다.
    /// 프리팹에 직렬화하지 않는다 — 맞는 동작은 어느 손님이든 같으니 FSM 모듈이 코드로 하나 만들어 쓴다.</summary>
    [Serializable]
    public sealed class HitReactState : CustomerState
    {
        private HashDataSO _clip;
        private float _duration;
        private CustomerState _then;

        /// <summary>몇 번 맞았는지. 움찔하는 도중에 또 맞으면 이 값이 바뀌어 처음부터 다시 움찔한다.</summary>
        private int _hitCount;

        /// <summary>때린 쪽의 위치. 움찔하는 동안 이쪽으로 몸을 돌린다.</summary>
        private Vector3 _hitFrom;

        /// <summary>때린 쪽으로 몸을 돌리는 빠르기(도/초). 움찔하는 짧은 사이에 다 돌아야 해서 빠르게 둔다.</summary>
        private const float TurnSpeed = 900f;

        /// <summary>이번에 틀 클립과 시간, 때린 쪽 위치, 끝나고 이어 할 행동을 정한다. 움찔하는 중이면 처음부터 다시 한다.</summary>
        public void Prepare(HashDataSO clip, float duration, Vector3 hitFrom, CustomerState then)
        {
            _clip = clip;
            _duration = Mathf.Max(0f, duration);
            _hitFrom = hitFrom;
            _then = then;
            _hitCount++;
        }

        /// <summary><paramref name="body"/>를 <paramref name="from"/> 쪽으로 이번 프레임만큼 돌린다. 다 돌았으면 true.</summary>
        public static bool TurnTowards(Transform body, Vector3 from, float maxDegrees)
        {
            Vector3 direction = from - body.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            body.rotation = Quaternion.RotateTowards(body.rotation, target, maxDegrees);
            return Quaternion.Angle(body.rotation, target) < 1f;
        }

        public override async UniTask<VisitOutcome> Run(CancellationToken ct)
        {
            AbstractCustomer customer = Ctx.Customer;
            IActionAnimator action = customer.ActionAnimator;

            try
            {
                int seen;
                do
                {
                    seen = _hitCount;

                    // 같은 클립을 다시 넣으면 처음부터 다시 튼다. 연달아 맞을 때마다 움찔한다.
                    if (action != null && _clip != null)
                    {
                        action.Begin(_clip);
                    }

                    // 걷던 중이면 그 자리에 선다. 안 멈추면 맞는 클립을 틀어 놓은 채 미끄러져 간다.
                    // 클립을 먼저 틀어야 이동 모듈이 서기 클립으로 덮지 않는다.
                    customer.Mover?.Stop();

                    // 움찔하면서 때린 쪽으로 돌아선다. 다 돌면 더 건드리지 않는다.
                    float until = Time.time + _duration;
                    bool facing = false;
                    while (Time.time < until && seen == _hitCount)
                    {
                        if (!facing)
                        {
                            facing = TurnTowards(customer.transform, _hitFrom, TurnSpeed * Time.deltaTime);
                        }

                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }
                }
                while (seen != _hitCount);
            }
            finally
            {
                // 취소로 끊겨도(죽음·반납) 반드시 접는다. 빼먹으면 다음에 걸을 때도 맞는 클립을 붙들고 있다.
                action?.End();
            }

            CustomerState then = _then;
            _then = null;
            return then != null ? await then.Run(ct) : VisitOutcome.Done;
        }

        public override void Reset()
        {
            _then = null;
        }
    }
}
