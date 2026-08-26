using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>
    /// NavMesh 경로를 코너 배열로 들고 있으면서 두 가지 질문에만 답하는 클래스.
    /// "차가 지금 경로의 어디쯤인가", "몇 미터 앞의 어느 점을 겨눠야 하는가".
    ///
    /// 조향각이나 속도는 계산하지 않는다. 그건 <see cref="CarSteeringSolver"/>의 몫이다.
    /// MonoBehaviour가 아니라 순수 C# 클래스라서, 경로 로직만 따로 떼어 읽고 고칠 수 있다.
    /// </summary>
    public sealed class CarPathTracker
    {
        private const int MaxCorners = 256;

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

        /// <summary>코너가 둘 이상 있어야 따라갈 선분이 생긴다.</summary>
        public bool HasPath => _cornerCount >= 2;

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

        /// <summary>
        /// 경유지에 충분히 가까워지면 마지막 구간을 직접 들고 간다.
        ///
        /// 경유지를 계속 에이전트의 목적지로 두면, 차가 Lookahead 때문에 경유지를 살짝 지나친 순간
        /// 경로가 되돌아갔다 다시 가는 헤어핀이 된다. 그 꺾이는 지점이 최소 회전원 안에 들어가면
        /// 차는 그 자리를 영원히 맴돈다.
        ///
        /// 또 에이전트 경로는 언제나 "현재 위치 → 목적지"라서 진입 방향이라는 정보가 사라진다.
        /// 그래서 진입점→목적지 직선을 직접 경로로 박아넣어 그 선을 추종하게 한다.
        /// 도착 방향이 자리 방향과 같아져 정차 뒤 제자리 회전이 사라진다.
        /// </summary>
        public void TrySwitchToFinalLeg(Vector3 position, float switchDistance, NavMeshAgent agent)
        {
            if (!_hasFinalPoint || agent == null || !agent.isOnNavMesh)
            {
                return;
            }

            if (HorizontalDistance(position, _viaPoint) > switchDistance)
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

        /// <summary>
        /// 에이전트가 계산해 둔 경로를 주기적으로 읽어온다.
        /// 매 프레임 읽지 않는 이유는 agent.path가 호출될 때마다 새 객체를 만들기 때문이다.
        /// </summary>
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

            // 진입점까지의 경로 뒤에 실제 목적지를 붙인다.
            // 두 번에 나눠 MoveTo하면 진입점에서 한 번 서버리며 오버슈트하지만,
            // 한 경로로 이어붙이면 속도를 유지한 채 매끄럽게 통과한다.
            if (_hasFinalPoint && _cornerCount > 0 && _cornerCount < MaxCorners)
            {
                _corners[_cornerCount] = _finalPoint;
                _cornerCount++;
            }

            _segIndex = 0;
        }

        /// <summary>
        /// 차를 경로에 투영해 현재 지점(커서)을 구하고, 지나온 세그먼트를 버린다.
        /// 인덱스를 앞으로만 밀기 때문에 경로가 자기 근처로 되돌아와도 뒷구간에 달라붙지 않는다.
        /// </summary>
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

        /// <summary>
        /// 경로를 따라 lookahead만큼 앞선 점.
        /// 경로 끝을 넘으면 마지막 구간 방향으로 더 뻗은 가상의 점을 돌려준다.
        ///
        /// 끝점 자체를 겨누면 차가 그 점으로 빨려들면서 방향이 틀어진 채 도착하고,
        /// 그 오차를 정차 뒤에 제자리 회전으로 메꾸게 된다.
        /// 선을 따라가게 두면 도착할 때 방향까지 맞는다.
        /// </summary>
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
