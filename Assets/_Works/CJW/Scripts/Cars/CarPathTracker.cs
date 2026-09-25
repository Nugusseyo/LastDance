using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>NavMesh 경로를 코너 배열로 들고 있으면서 "지금 경로의 어디쯤인가", "몇 미터 앞을 겨눠야 하는가"에만 답하는 클래스. 조향각·속도 계산은 <see cref="CarSteeringSolver"/>의 몫이다.</summary>
    public sealed class CarPathTracker
    {
        private const int MaxCorners = 256;

        /// <summary>이 거리 안으로 경유지에 붙었으면 각도를 따지지 않고 전환한다(m).</summary>
        private const float NearViaDistance = 1.5f;

        private readonly Vector3[] _corners = new Vector3[MaxCorners];
        private int _cornerCount;
        private int _segIndex;
        private float _refreshTimer;

        /// <summary>경로 끝에 덧붙일 실제 목적지. 진입점을 거쳐 자리로 들어올 때만 쓴다.</summary>
        private bool _hasFinalPoint;
        private Vector3 _finalPoint;

        /// <summary>경유지(진입점).</summary>
        private Vector3 _viaPoint;

        private bool _onFinalLeg;

        public int CornerCount => _cornerCount;
        public int SegmentIndex => _segIndex;

        /// <summary>기즈모용. 읽기 전용으로만 쓴다.</summary>
        public Vector3[] Corners => _corners;

        /// <summary>진입점을 지나 마지막 직선 구간을 달리는 중인지.</summary>
        public bool OnFinalLeg => _onFinalLeg;

        /// <summary>아직 진입점을 지나지 않아 마지막 직선 구간이 남아 있는지.</summary>
        public bool HasPendingFinalLeg => _hasFinalPoint;

        /// <summary>마지막 직선 구간이 시작되는 진입점.</summary>
        public Vector3 ViaPoint => _viaPoint;

        /// <summary>코너가 둘 이상 있어야 따라갈 선분이 생긴다.</summary>
        public bool HasPath => _cornerCount >= 2;

        /// <summary>마지막으로 읽은 경로의 상태. PathComplete가 아니면 목적지에 닿지 못한다는 뜻이다.</summary>
        public NavMeshPathStatus Status { get; private set; } = NavMeshPathStatus.PathComplete;

        /// <summary>정차. 들고 있던 경로를 통째로 버린다.</summary>
        public void Clear()
        {
            _cornerCount = 0;
            _segIndex = 0;
            _refreshTimer = 0f;
            _hasFinalPoint = false;
            _onFinalLeg = false;
        }

        /// <summary>새 목적지를 향해 출발할 때. 예약해 둔 진입점 정보는 그대로 남긴다.</summary>
        public void BeginPath()
        {
            _cornerCount = 0;
            _segIndex = 0;
            _refreshTimer = 0f;
            _onFinalLeg = false;

            // 아직 경로를 읽기 전이다. 이전 목적지의 판정을 끌고 가지 않는다.
            Status = NavMeshPathStatus.PathComplete;
        }

        /// <summary>진입점을 거쳐 destination으로 들어가겠다고 예약한다. BeginPath보다 먼저 부른다.</summary>
        public void SetApproach(Vector3 destination, Vector3 via)
        {
            _finalPoint = destination;
            _viaPoint = via;
            _hasFinalPoint = true;
        }

        /// <summary>진입점 없이 곧장 간다.</summary>
        public void ClearApproach()
        {
            _hasFinalPoint = false;
        }

        /// <summary>경유지에 충분히 가까워지고 방향까지 맞으면 마지막 구간을 직선 경로로 직접 박아넣는다. 에이전트 목적지로만 두면 Lookahead 때문에 경유지를 살짝 지나쳐 헤어핀 경로가 생기기 때문이다.</summary>
        public void TrySwitchToFinalLeg(Vector3 position, Vector3 forward, float switchDistance, NavMeshAgent agent)
        {
            if (!_hasFinalPoint || agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            float distance = HorizontalDistance(position, _viaPoint);

            if (distance > switchDistance)
            {
                return;
            }

            if (distance > NearViaDistance && !IsAlignedWithFinalLeg(forward))
            {
                return;
            }

            _hasFinalPoint = false;
            _onFinalLeg = true;

            // 이동은 아래 직선을 따르지만, 다른 에이전트의 회피는 여전히 에이전트 기준으로 돌아간다.
            agent.SetDestination(_finalPoint);

            _corners[0] = _viaPoint;
            _corners[1] = _finalPoint;
            _cornerCount = 2;
            _segIndex = 0;
        }

        /// <summary>차 머리가 마지막 직선 방향과 얼추나마 맞는지(60도 이내).</summary>
        private bool IsAlignedWithFinalLeg(Vector3 forward)
        {
            Vector3 leg = _finalPoint - _viaPoint;
            leg.y = 0f;
            forward.y = 0f;

            if (leg.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f)
            {
                return true;
            }

            return Vector3.Dot(forward.normalized, leg.normalized) >= 0.5f;
        }

        /// <summary>에이전트가 계산해 둔 경로를 주기적으로 읽어온다. 매 프레임 읽지 않는 이유는 agent.path가 호출될 때마다 새 객체를 만들기 때문이다.</summary>
        public void RefreshIfNeeded(NavMeshAgent agent, float dt, float interval)
        {
            // 마지막 직선 구간은 직접 박아둔 경로다. 에이전트 경로로 덮어쓰지 않는다.
            if (_onFinalLeg || agent == null)
            {
                return;
            }

            _refreshTimer -= dt;

            if (HasPath && _refreshTimer > 0f)
            {
                return;
            }

            if (agent.pathPending)
            {
                return;
            }

            _refreshTimer = interval;

            NavMeshPath path = agent.path;
            if (path == null)
            {
                _cornerCount = 0;
                return;
            }

            _cornerCount = path.GetCornersNonAlloc(_corners);
            Status = path.status;

            // 진입점까지의 경로 뒤에 실제 목적지를 붙인다.
            // 두 번에 나눠 MoveTo하면 진입점에서 한 번 서버리며 오버슈팅하지만,
            // 한 경로로 이어붙이면 속도를 유지한 채 매끄럽게 통과한다.
            //
            // 단, 경로가 진입점에 닿지도 못했는데(부분 경로) 목적지를 이어붙이면
            // 경로 끝에서 자리까지 벽을 관통하는 직선이 생긴다. 그때는 붙이지 않는다.
            if (_hasFinalPoint && Status == NavMeshPathStatus.PathComplete
                && _cornerCount > 0 && _cornerCount < MaxCorners)
            {
                _corners[_cornerCount] = _finalPoint;
                _cornerCount++;
            }

            _segIndex = 0;
        }

        /// <summary>차를 경로에 투영해 현재 지점(커서)을 구하고, 지나온 세그먼트를 버린다. 인덱스를 앞으로만 밀어 경로가 되돌아와도 뒷구간에 달라붙지 않는다.</summary>
        public Vector3 Advance(Vector3 position, out float remaining)
        {
            while (_segIndex < _cornerCount - 2)
            {
                if (ProjectOnSegment(_corners[_segIndex], _corners[_segIndex + 1], position) < 0.999f)
                {
                    break;
                }

                _segIndex++;
            }

            float t = ProjectOnSegment(_corners[_segIndex], _corners[_segIndex + 1], position);
            Vector3 cursor = Vector3.Lerp(_corners[_segIndex], _corners[_segIndex + 1], t);

            remaining = HorizontalDistance(cursor, _corners[_segIndex + 1]);
            for (int i = _segIndex + 1; i < _cornerCount - 1; i++)
            {
                remaining += HorizontalDistance(_corners[i], _corners[i + 1]);
            }

            return cursor;
        }

        /// <summary>경로를 따라 lookahead만큼 앞선 점. 끝점 자체를 겨누면 도착 방향이 틀어져 제자리 회전이 생기므로, 경로 끝을 넘으면 마지막 구간 방향으로 더 뻗은 가상의 점을 돌려준다.</summary>
        public Vector3 FindGoalPoint(Vector3 cursor, float lookahead)
        {
            float left = lookahead;
            Vector3 from = cursor;
            Vector3 goal = _corners[_cornerCount - 1];

            for (int i = _segIndex + 1; i < _cornerCount; i++)
            {
                Vector3 to = _corners[i];
                float d = HorizontalDistance(from, to);

                if (d >= left)
                {
                    return Vector3.Lerp(from, to, left / Mathf.Max(d, 1e-4f));
                }

                left -= d;
                from = to;
                goal = to;
            }

            // 경로를 다 쓰고도 lookahead가 남았다. 마지막 구간 방향으로 그만큼 더 뻗는다.
            Vector3 tail = _corners[_cornerCount - 1] - _corners[Mathf.Max(_cornerCount - 2, 0)];
            tail.y = 0f;

            if (tail.sqrMagnitude > 1e-6f)
            {
                goal = _corners[_cornerCount - 1] + tail.normalized * left;
            }

            return goal;
        }

        /// <summary>점 p를 선분 a→b 위에 투영했을 때의 위치를 0~1 비율로 돌려준다. 높이는 무시한다.</summary>
        private static float ProjectOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            ab.y = 0f;

            float lengthSqr = ab.sqrMagnitude;
            if (lengthSqr < 1e-6f)
            {
                return 1f;
            }

            Vector3 ap = p - a;
            ap.y = 0f;

            return Mathf.Clamp01(Vector3.Dot(ap, ab) / lengthSqr);
        }

        /// <summary>높이를 무시한 평면 거리.</summary>
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
