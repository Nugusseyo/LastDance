using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.MapSystems;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Visit.States
{
    /// <summary>차를 세우지 않고 경유지들을 돌고 또 돈다. 뺑뺑 도는 손님이 이 연출이다.
    /// <see cref="ArrivingState"/> 자리에 <see cref="MapSystems.CarDataSO"/>의 연출 덮어쓰기로 꽂아 쓴다.
    /// 스스로 멈추는 것은 <see cref="lapTimeout"/>이 다 됐을 때뿐이고, 보통은 퇴치(<see cref="VisitSession.Repel"/>)가
    /// 먼저 이 단계를 끊는다 — 안전망을 남겨 두지 않으면 퇴치가 연결되기 전까지 주차 자리가 영영 반납되지 않는다.</summary>
    [Serializable]
    public sealed class CirclingState : VisitState
    {
        [Tooltip("경유지를 물어볼 맵 데이터. 씬의 MapPosition들이 등록하는 그 에셋이다.")]
        [SerializeField] private MapDataSo mapData;

        [Tooltip("이 종류의 지점을 등록된 순서대로 돈다. 두 개 이상 놓아야 순회가 된다.")]
        [SerializeField] private MapPointType patrolPoint = MapPointType.Patrol;

        [Tooltip("이만큼 돌고 나면 스스로 그만두고 다음 단계로 넘어간다(초). 퇴치가 없을 때의 안전망이다.")]
        [SerializeField, Min(0f)] private float lapTimeout = 120f;

        [Tooltip("경유지에서 이 반경(m) 안에 들어오면 닿은 것으로 보고 다음 경유지로 넘어간다.")]
        [SerializeField, Min(0.5f)] private float pointReachRadius = 4f;

        [Tooltip("한 경유지에 이 시간을 쓰고도 못 닿으면 포기하고 다음 경유지로 넘어간다(초).")]
        [SerializeField, Min(1f)] private float pointTimeout = 20f;

        public override VisitPhase Phase => VisitPhase.Arriving;

        public override void Enter(VisitContext context)
        {
            if (mapData == null)
            {
                // 여기서 막지 않으면 차가 스폰 자리에 선 채로 lapTimeout을 통째로 흘려보낸다.
                Debug.LogError($"[{nameof(CirclingState)}] MapData를 지정해야 합니다. 순회하지 않고 제자리에 섭니다.", context.Car);
                return;
            }

            // 가장 가까운 경유지부터 돈다. 스폰 직후 먼 경유지로 가느라 주차장을 가로지르지 않게 한다.
            context.PatrolIndex = NearestIndex(context.Car.transform.position);
            MoveToCurrent(context);
        }

        public override VisitPhase Tick(VisitContext context, float dt)
        {
            context.PhaseElapsed += dt;
            context.PatrolPointElapsed += dt;

            if (lapTimeout > 0f && context.PhaseElapsed >= lapTimeout)
            {
                // 다 돌았으면 그 자리에 서지 않고 곧장 떠난다. 도로 한가운데서 하차·대기로 넘어가면
                // 길을 막고 수십 초를 서 있게 된다. 멈추지 않고 이어 달리도록 Stop()도 부르지 않는다 —
                // 퇴장 단계가 새 목적지를 주면 지금 속도 그대로 방향만 바꾼다.
                return VisitPhase.Leaving;
            }

            if (mapData == null)
            {
                return VisitPhase.Arriving;
            }

            // 경유지는 지나가는 점이라 정확히 닿을 필요가 없다. 가까워지면 곧장 다음으로 넘겨,
            // 모서리를 조금 비껴 지나친 차가 그 점을 맞추려고 빙빙 돌거나 서지 않게 한다.
            bool arrived = context.Car.IsArrived || IsNearCurrent(context);

            // 길이 닿지 않는 경유지에서 곧장 다음으로 넘기면, 전부 닿지 않을 때 매 프레임 목록을 헛돈다.
            // 시간을 조금이라도 쓰게 해서 한 바퀴가 한 프레임에 지나가지 않도록 한다.
            bool givenUp = context.PatrolPointElapsed >= pointTimeout ||
                           (!context.Car.HasCompletePath && context.PatrolPointElapsed >= 1f);

            if (arrived || givenUp)
            {
                context.PatrolIndex++;
                MoveToCurrent(context);
            }

            return VisitPhase.Arriving;
        }

        /// <summary>지금 커서가 가리키는 경유지로 차를 보낸다. 커서는 목록 길이를 넘어도 되며,
        /// <see cref="MapDataSo.TryGetAt"/>이 처음으로 돌려 준다 — 그래서 끝없이 돈다.</summary>
        private void MoveToCurrent(VisitContext context)
        {
            context.PatrolPointElapsed = 0f;

            if (!TryGetOrdered(context.PatrolIndex, out MapPosition point))
            {
                // 경유지가 하나도 없다. 순회할 수 없으니 제자리에 서서 안전망(lapTimeout)을 기다린다.
                Debug.LogWarning($"[{nameof(CirclingState)}] 맵에 {patrolPoint} 지점이 없어 순회하지 못합니다. 씬에 MapPosition을 놓아야 합니다.", context.Car);
                return;
            }

            context.Car.MoveTo(point.Position);
        }
    
        /// <summary>경유지를 등록 순서가 아니라 둘레를 따라 반시계 방향으로 정렬한 목록. 등록 순서는 OnEnable 순서라
        /// 씬마다 달라서, 그대로 돌면 사각형 대각선을 가로지르는 8자 경로가 나온다.</summary>
        private static readonly List<MapPosition> _ordered = new();

        private bool TryGetOrdered(int index, out MapPosition point)
        {
            point = null;
            BuildOrdered();

            if (_ordered.Count == 0)
            {
                return false;
            }

            int wrapped = ((index % _ordered.Count) + _ordered.Count) % _ordered.Count;
            point = _ordered[wrapped];
            return true;
        }

        private bool IsNearCurrent(VisitContext context)
        {
            if (!TryGetOrdered(context.PatrolIndex, out MapPosition point))
            {
                return false;
            }

            Vector3 delta = point.Position - context.Car.transform.position;
            delta.y = 0f;
            return delta.magnitude <= pointReachRadius;
        }

        private int NearestIndex(Vector3 from)
        {
            BuildOrdered();

            int best = 0;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _ordered.Count; i++)
            {
                Vector3 delta = _ordered[i].Position - from;
                delta.y = 0f;
                if (delta.sqrMagnitude < bestDistance)
                {
                    bestDistance = delta.sqrMagnitude;
                    best = i;
                }
            }

            return best;
        }

        private void BuildOrdered()
        {
            _ordered.Clear();

            if (mapData == null)
            {
                return;
            }

            IReadOnlyList<MapPosition> all = mapData.GetAll(patrolPoint);
            Vector3 center = Vector3.zero;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                {
                    _ordered.Add(all[i]);
                    center += all[i].Position;
                }
            }

            if (_ordered.Count == 0)
            {
                return;
            }

            center /= _ordered.Count;

            // 위에서 본 각도(x 오른쪽, z 위)가 커지는 쪽이 반시계 방향이다.
            _ordered.Sort((a, b) => Angle(a.Position, center).CompareTo(Angle(b.Position, center)));
        }

        private static float Angle(Vector3 point, Vector3 center) => Mathf.Atan2(point.z - center.z, point.x - center.x);
    }
}
