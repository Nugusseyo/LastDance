using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>차체 앞(후진 중이면 뒤)으로 차 폭만큼의 차로를 긋고, 그 안에 들어온 다른 차까지의 거리를 잰다.
    /// 달리는 중에 차로 안에 차가 잡히면 곧바로 좌우 중 빈 쪽(오른쪽 먼저)을 골라 옆으로 비켜 지나간다.
    /// 이동 모듈이 에이전트 위치를 직접 굴리므로 NavMeshAgent의 회피가 먹지 않는다. 차끼리 겹치지 않게 하는 건 이 센서뿐이다.</summary>
    public class CarTrafficSensorModule : AbstractModule, ICarTrafficSensor
    {
        private enum BypassPhase
        {
            None,
            /// <summary>옆으로 비켜 앞차를 지나가는 중.</summary>
            Passing,
            /// <summary>다 지났거나 포기해서 원래 경로로 돌아오는 중.</summary>
            Returning,
        }

        /// <summary>한 번 계산해 여러 곳에서 쓰는 이 차의 현재 자세.</summary>
        private struct Frame
        {
            public Vector3 Center;
            public Vector3 Right;
            public Vector3 Forward;
            public Vector2 Half;
        }

        [Tooltip("차체 크기를 읽어올 NavMeshObstacle(Box). 비워두면 자식에서 찾는다. 손님이 피해 가는 크기와 같은 값을 쓰려는 것이다.")]
        [SerializeField] private NavMeshObstacle footprint;

        [Tooltip("footprint가 없을 때 쓰는 차체 반폭(x)·반길이(y)(m).")]
        [SerializeField] private Vector2 fallbackHalfSize = new(1f, 2.3f);

        [Tooltip("차로를 차 폭보다 양옆으로 이만큼 넓게 본다(m). 스치듯 지나가며 부딪히는 것을 막는다.")]
        [SerializeField, Min(0f)] private float laneMargin = 0.3f;

        [Header("추월")]
        [Tooltip("끄면 앞차가 비킬 때까지 뒤에서 기다리기만 한다.")]
        [SerializeField] private bool enableBypass = true;

        [Tooltip("앞차 옆면에서 이만큼 더 떨어져 지나간다(m).")]
        [SerializeField, Min(0f)] private float bypassSideMargin = 0.5f;

        [Tooltip("옆으로 이보다 많이 비켜야 하면 추월하지 않는다(m). 도로 폭보다 크게 두면 반대편 차로로 넘어간다.")]
        [SerializeField, Min(0.5f)] private float maxBypassOffset = 4f;

        [Tooltip("옆으로 옮겨가는 속도(m/s). 크면 급하게 핸들을 꺾는다.")]
        [SerializeField, Min(0.1f)] private float bypassShiftSpeed = 1.5f;

        [Tooltip("앞차 앞 범퍼를 이만큼 지나야 원래 경로로 돌아온다(m).")]
        [SerializeField, Min(0f)] private float bypassReturnGap = 1.5f;

        [Tooltip("이 시간(초) 안에 다 지나가지 못하면 포기하고 돌아온다. 옆에서 영영 나란히 서 있지 않게 한다.")]
        [SerializeField, Min(0.5f)] private float maxBypassTime = 8f;

        [Tooltip("양옆이 다 막혀 추월하지 못했을 때 다시 살피기까지의 간격(초). 매 프레임 같은 검사를 반복하지 않게 한다.")]
        [SerializeField, Min(0f)] private float bypassRetryInterval = 0.3f;

        [Tooltip("다른 차가 지금 속도로 이 시간(초) 뒤에 있을 자리까지 내 차로에 걸리는지 본다. 옆에서 끼어드는 차를 미리 보고 선다.")]
        [SerializeField, Min(0f)] private float predictionTime = 1.5f;

        [Tooltip("차체 정면 방향으로 이 거리(m) 안에 든 차는 목표점 방향과 상관없이 막힌 것으로 본다. 핸들을 꺾는 중에 정면 차를 놓치지 않게 한다.")]
        [SerializeField, Min(0f)] private float bodyLaneRange = 2.5f;

        /// <summary>예측 구간을 몇 번 나눠 볼지. 한 번만 보면 빠르게 가로지르는 차가 그 사이를 빠져나간다.</summary>
        private const int PredictionSteps = 2;

        [Tooltip("내 차체 옆면에서 이 거리(m) 안에서 NavMesh가 끊긴 것은 내 NavMeshObstacle이 낸 구멍으로 본다. " +
                 "NavMesh 베이크 반경보다 조금 크게 둔다.")]
        [SerializeField, Min(0f)] private float ownCarveAllowance = 1f;

        [Tooltip("옆 차로 시작점을 NavMesh 위로 붙일 때 찾아볼 반경(m).")]
        [SerializeField, Min(0.05f)] private float sideSampleRadius = 0.5f;

        [Tooltip("켜면 추월을 시작하거나 못 하는 이유를 콘솔에 남긴다.")]
        [SerializeField] private bool debugBypass;

        private readonly Vector3[] _otherCorners = new Vector3[4];
        private readonly Vector3[] _predictedCorners = new Vector3[4];

        private Vector3 _lastCenter;
        private bool _hasLastCenter;
        private Vector3 _velocity;

        private ICarTrafficSensor _blocker;
        private int _blockerFrame = -1;

        private BypassPhase _phase;
        private ICarTrafficSensor _bypassTarget;
        private Vector3 _bypassDir;
        private float _bypassWidth;
        private float _offset;
        private float _bypassTimer;
        private float _cooldownTimer;

        public ICarTrafficSensor Blocker => _blockerFrame >= Time.frameCount - 1 ? _blocker : null;

        public Vector3 AvoidanceOffset => _phase == BypassPhase.None ? Vector3.zero : _bypassDir * _offset;

        public int Priority => GetInstanceID();

        public Vector3 Center => FootprintCenter();

        public Vector3 Velocity => _velocity;

        public float BoundingRadius => HalfSize().magnitude;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            if (footprint == null)
            {
                footprint = GetComponentInChildren<NavMeshObstacle>(true);
            }

            if (footprint != null && footprint.shape != NavMeshObstacleShape.Box)
            {
                Debug.LogWarning($"[CarTraffic] {name}의 NavMeshObstacle이 Box가 아니라 차체 크기를 반경으로 어림합니다.", this);
            }
        }

        private void OnEnable()
        {
            CarTraffic.Register(this);
            _hasLastCenter = false;
            _velocity = Vector3.zero;
        }

        /// <summary>속도는 이동 모듈이 아니라 실제 위치 변화로 잰다. 후진·정렬 회전·풀에서 순간이동한 것까지 그대로 반영된다.</summary>
        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector3 center = FootprintCenter();

            if (_hasLastCenter && dt > 1e-5f)
            {
                Vector3 raw = (center - _lastCenter) / dt;

                // 스폰할 때처럼 한 프레임에 크게 뛴 건 속도가 아니다.
                if (raw.sqrMagnitude > 900f)
                {
                    raw = Vector3.zero;
                }

                _velocity = Vector3.Lerp(_velocity, raw, 0.3f);
            }

            _lastCenter = center;
            _hasLastCenter = true;
        }

        private void OnDisable()
        {
            CarTraffic.Unregister(this);
            _blocker = null;

            // 풀에서 다시 나올 때 이전 방문의 추월을 이어가지 않는다.
            CancelBypass();
        }

        public float ClearDistance(bool reversing, float range, bool allowBypass, Vector3 travelDirection)
        {
            float dt = Time.deltaTime;

            // 차로는 차체 정면이 아니라 실제로 나아갈 쪽으로 긋는다. 정면으로 그으면 경로가 이미 피해서 휘어 있는
            // 주차된 차를 "앞차"로 보고 서 버린다. 후진은 차체 뒤쪽이 곧 나아갈 쪽이다.
            Frame body = CurrentFrame();
            Frame frame = LaneFrame(body, reversing ? -body.Forward : travelDirection);
            Frame bodyLane = LaneFrame(body, reversing ? -body.Forward : body.Forward);

            UpdateBypass(frame, allowBypass && enableBypass && !reversing, dt);

            const float dir = 1f;
            float laneHalf = frame.Half.x + laneMargin;

            float best = float.PositiveInfinity;
            ICarTrafficSensor blocker = null;

            var sensors = CarTraffic.Sensors;
            for (int i = 0; i < sensors.Count; i++)
            {
                ICarTrafficSensor other = sensors[i];
                if (ReferenceEquals(other, this))
                {
                    continue;
                }

                Vector3 toOther = other.Center - frame.Center;
                toOther.y = 0f;
                // 빠르게 다가오는 차는 예측한 자리까지 봐야 하므로 그만큼 더 넓게 거른다.
                float reach = other.Velocity.magnitude * predictionTime;
                if (toOther.magnitude > frame.Half.y + range + other.BoundingRadius + reach)
                {
                    continue;
                }

                // 서로 기다리는 교착이어도 상대를 무시하지 않는다. 무시하면 차로에 걸친 차를 그대로 뚫고 지나간다.
                // 교착은 추월과 물러서기(이동 모듈)로 푼다.
                //
                // 추월 중인 상대도 무시하지 않는다. 차로가 목표점(옆으로 민 쪽)을 향하므로, 충분히 비켜 나면
                // 저절로 차로에서 빠진다. 다만 여유 폭 없이 차 폭 그대로 봐서 스치듯 지나가게 둔다.
                bool passingTarget = _phase == BypassPhase.Passing && ReferenceEquals(other, _bypassTarget);
                float half = passingTarget ? frame.Half.x : laneHalf;

                other.GetCorners(_otherCorners);
                float gap = GapInLane(_otherCorners, frame, dir, half, 0f);

                // 차는 지금 이 순간엔 차체 정면으로 움직인다. 목표점 쪽과 차체가 크게 틀어져 있으면 정면의 차를 놓치므로
                // 바로 앞 짧은 거리만은 차체 방향으로도 본다. 멀리까지 보면 경로가 피해 가는 주차차에 또 서 버린다.
                float bodyGap = GapInLane(_otherCorners, bodyLane, dir, bodyLane.Half.x + laneMargin, 0f);
                if (bodyGap < bodyLaneRange)
                {
                    gap = Mathf.Min(gap, bodyGap);
                }

                // 옆에서 끼어드는 차는 차로에 들어온 뒤에 보면 이미 늦다. 상대 속도로 조금 뒤의 자리를 미리 본다.
                // 다만 교차하는 두 차가 서로를 예측해 둘 다 서면 안 되므로, 예측만으로 겹치는 경우엔
                // 우선순위가 낮은 쪽만 양보한다. 지금 이미 차로 안에 있는 차는 우선순위와 상관없이 본다.
                Vector3 velocity = other.Velocity;
                velocity.y = 0f;
                if (velocity.sqrMagnitude > 0.04f && Priority < other.Priority)
                {
                    for (int s = 1; s <= PredictionSteps; s++)
                    {
                        Vector3 shift = velocity * (predictionTime * s / PredictionSteps);
                        for (int c = 0; c < 4; c++)
                        {
                            _predictedCorners[c] = _otherCorners[c] + shift;
                        }

                        gap = Mathf.Min(gap, GapInLane(_predictedCorners, frame, dir, half, 0f));
                    }
                }

                if (gap < best)
                {
                    best = gap;
                    blocker = other;
                }
            }

            if (best > range)
            {
                best = float.PositiveInfinity;
                blocker = null;
            }

            _blocker = blocker;
            _blockerFrame = Time.frameCount;

            TryStartBypass(frame, blocker, allowBypass && enableBypass && !reversing);

            return best;
        }

        /// <summary>추월 중이면 옆으로 옮겨가고, 끝낼 때가 되면 돌아온다.</summary>
        private void UpdateBypass(Frame frame, bool allowed, float dt)
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= dt;
            }

            switch (_phase)
            {
                case BypassPhase.Passing:
                    _bypassTimer += dt;
                    _offset = Mathf.MoveTowards(_offset, _bypassWidth, bypassShiftSpeed * dt);

                    bool lost = !CarTraffic.IsActive(_bypassTarget);
                    if (lost || !allowed || _bypassTimer > maxBypassTime || HasPassed(frame, _bypassTarget))
                    {
                        _phase = BypassPhase.Returning;
                    }
                    break;

                case BypassPhase.Returning:
                    _offset = Mathf.MoveTowards(_offset, 0f, bypassShiftSpeed * dt);
                    if (_offset <= 0f)
                    {
                        // 돌아오자마자 앞에 또 차가 있으면 바로 다시 비켜 간다.
                        EndBypass(0f);
                    }
                    break;
            }
        }

        /// <summary>진행 방향 차로에 차가 잡히면 곧바로 빈 쪽을 골라 추월을 시작한다. 기다렸다가 비키지 않는다.</summary>
        private void TryStartBypass(Frame frame, ICarTrafficSensor blocker, bool allowed)
        {
            if (_phase != BypassPhase.None || _cooldownTimer > 0f || blocker == null || !allowed)
            {
                return;
            }

            if (!TryPickSide(frame, blocker, out Vector3 dir, out float width))
            {
                // 양쪽 다 막혔다. 그 자리에서 앞차 뒤에 서고, 잠시 뒤 다시 살핀다.
                _cooldownTimer = bypassRetryInterval;
                return;
            }

            LogBypass($"{width:F1}m 비켜 추월 시작");

            _phase = BypassPhase.Passing;
            _bypassTarget = blocker;
            _bypassDir = dir;
            _bypassWidth = width;
            _bypassTimer = 0f;
            _offset = 0f;
        }

        /// <summary>앞차 좌우로 비켜 지나갈 수 있는지 본다. 덜 비켜도 되는 쪽부터 확인한다.</summary>
        private bool TryPickSide(Frame frame, ICarTrafficSensor blocker, out Vector3 dir, out float width)
        {
            dir = Vector3.zero;
            width = 0f;

            blocker.GetCorners(_otherCorners);

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxZ = float.NegativeInfinity;

            for (int i = 0; i < 4; i++)
            {
                Vector3 local = _otherCorners[i] - frame.Center;
                float x = Vector3.Dot(local, frame.Right);
                float z = Vector3.Dot(local, frame.Forward);
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                maxZ = Mathf.Max(maxZ, z);
            }

            float right = maxX + frame.Half.x + bypassSideMargin;
            float left = minX - frame.Half.x - bypassSideMargin;

            // 앞차 앞 범퍼 너머, 내 차가 통째로 들어갈 자리까지 비어 있어야 한다.
            float passLength = maxZ + frame.Half.y + bypassReturnGap;

            // 항상 오른쪽부터 본다. 마주 오는 두 차가 각자 오른쪽으로 비켜야 서로 스쳐 지나간다.
            // 덜 비키는 쪽을 고르면 둘이 같은 쪽으로 비켜 정면으로 다시 만난다.
            float first = right;
            float second = left;

            if (IsSideClear(frame, blocker, first, passLength))
            {
                dir = frame.Right * Mathf.Sign(first);
                width = Mathf.Abs(first);
                return true;
            }

            if (IsSideClear(frame, blocker, second, passLength))
            {
                dir = frame.Right * Mathf.Sign(second);
                width = Mathf.Abs(second);
                return true;
            }

            return false;
        }

        /// <summary>옆으로 shift만큼 옮긴 차로가 NavMesh 위에 있고 다른 차에 막혀 있지 않은지.</summary>
        private bool IsSideClear(Frame frame, ICarTrafficSensor blocker, float shift, float passLength)
        {
            if (Mathf.Abs(shift) > maxBypassOffset)
            {
                LogBypass($"옆으로 {Mathf.Abs(shift):F1}m 비켜야 해서 한계({maxBypassOffset:F1}m)를 넘음");
                return false;
            }

            // 바닥 높이는 차 루트가 NavMesh 위에 있다. 차체 중심(footprint)은 떠 있을 수 있어 쓰지 않는다.
            // 차 피벗은 바퀴가 바닥에 닿도록 떠 있을 수 있다. 옆 차로는 NavMesh 면 높이에서 그어야 SamplePosition이 잡힌다.
            Vector3 origin = transform.position;
            if (CarNavMesh.SamplePosition(origin, out NavMeshHit ground, 2f))
            {
                origin = ground.position;
            }
            Vector3 side = origin + frame.Right * shift;

            if (!CarNavMesh.SamplePosition(side, out NavMeshHit sideHit, sideSampleRadius))
            {
                LogBypass($"옆 차로 시작점({shift:+0.0;-0.0}m)이 NavMesh 밖");
                return false;
            }

            side = sideHit.position;

            // 멈춰 선 차는 자기 NavMeshObstacle이 발밑에 구멍을 낸다. 내 위치에서 레이를 쏘면 그 구멍에 바로 막히므로
            // 옆 차로에서 나를 향해 쏘고, 내 차체 근처에서 막힌 것은 내 구멍으로 보고 넘긴다.
            if (CarNavMesh.Raycast(side, origin, out NavMeshHit backHit))
            {
                float fromMe = Vector3.Dot(backHit.position - origin, frame.Right) * Mathf.Sign(shift);
                if (fromMe > frame.Half.x + ownCarveAllowance)
                {
                    LogBypass($"옆 차로({shift:+0.0;-0.0}m)로 넘어가는 길이 {fromMe:F1}m 지점에서 끊김");
                    return false;
                }
            }

            if (CarNavMesh.Raycast(side, side + frame.Forward * passLength, out NavMeshHit forwardHit))
            {
                LogBypass($"옆 차로({shift:+0.0;-0.0}m)가 {forwardHit.distance:F1}m 앞에서 끊김 (필요 {passLength:F1}m)");
                return false;
            }

            var sensors = CarTraffic.Sensors;
            for (int i = 0; i < sensors.Count; i++)
            {
                ICarTrafficSensor other = sensors[i];
                if (ReferenceEquals(other, this) || ReferenceEquals(other, blocker))
                {
                    continue;
                }

                other.GetCorners(_otherCorners);

                // 옆 차로는 뒤에서 오는 차도 봐야 한다. 끼어드는 순간 뒤차가 들이받는다.
                if (IntersectsLaneSpan(_otherCorners, frame, frame.Half.x + laneMargin, shift,
                                       -frame.Half.y, passLength))
                {
                    LogBypass($"옆 차로({shift:+0.0;-0.0}m)에 다른 차가 있음");
                    return false;
                }
            }

            return true;
        }

        /// <summary>추월 상대의 앞 범퍼가 내 뒤 범퍼보다 bypassReturnGap 이상 뒤로 갔는지.</summary>
        private bool HasPassed(Frame frame, ICarTrafficSensor target)
        {
            target.GetCorners(_otherCorners);

            for (int i = 0; i < 4; i++)
            {
                float z = Vector3.Dot(_otherCorners[i] - frame.Center, frame.Forward);
                if (z > -frame.Half.y - bypassReturnGap)
                {
                    return false;
                }
            }

            return true;
        }

        private void LogBypass(string message)
        {
            if (debugBypass)
            {
                Debug.Log($"[CarTraffic] {name}: {message}", this);
            }
        }

        public void CancelBypass()
        {
            EndBypass(0f);
        }

        private void EndBypass(float cooldown)
        {
            _phase = BypassPhase.None;
            _bypassTarget = null;
            _offset = 0f;
            _bypassTimer = 0f;
            _cooldownTimer = cooldown;
        }

        /// <summary>상대 차체의 네 변을 내 차로 폭으로 잘라, 내 범퍼에서 가장 가까운 지점까지의 거리를 구한다.
        /// 모서리만 보면 옆으로 가로막은 긴 차의 옆면을 놓치므로 변 단위로 본다. shift만큼 옆으로 옮긴 차로도 잴 수 있다.</summary>
        private static float GapInLane(Vector3[] corners, Frame frame, float dir, float laneHalf, float shift)
        {
            float best = float.PositiveInfinity;

            for (int i = 0; i < 4; i++)
            {
                if (!ClipEdge(corners[i], corners[(i + 1) % 4], frame, dir, laneHalf, shift, out float za, out float zb))
                {
                    continue;
                }

                // 내 중심보다 뒤에 있는 부분은 보지 않는다. 겹쳐 나온 뒤차를 앞차가 기다리면 둘 다 영영 못 간다.
                if (Mathf.Max(za, zb) <= 0f)
                {
                    continue;
                }

                float nearest = Mathf.Max(Mathf.Min(za, zb), 0f);
                best = Mathf.Min(best, Mathf.Max(nearest - frame.Half.y, 0f));
            }

            return best;
        }

        /// <summary>옆으로 shift만큼 옮긴 차로의 [zMin, zMax] 구간에 상대 차체가 걸치는지.</summary>
        private static bool IntersectsLaneSpan(Vector3[] corners, Frame frame, float laneHalf, float shift,
                                               float zMin, float zMax)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!ClipEdge(corners[i], corners[(i + 1) % 4], frame, 1f, laneHalf, shift, out float za, out float zb))
                {
                    continue;
                }

                if (Mathf.Max(za, zb) >= zMin && Mathf.Min(za, zb) <= zMax)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>변 하나를 차로 폭 안쪽만 남기고 잘라, 남은 부분 양 끝의 전후 좌표를 돌려준다.</summary>
        private static bool ClipEdge(Vector3 p0, Vector3 p1, Frame frame, float dir, float laneHalf, float shift,
                                     out float za, out float zb)
        {
            Vector3 a = p0 - frame.Center;
            Vector3 b = p1 - frame.Center;

            float x0 = Vector3.Dot(a, frame.Right) - shift;
            float x1 = Vector3.Dot(b, frame.Right) - shift;
            float z0 = Vector3.Dot(a, frame.Forward) * dir;
            float z1 = Vector3.Dot(b, frame.Forward) * dir;

            za = zb = 0f;

            float t0 = 0f;
            float t1 = 1f;
            float dx = x1 - x0;

            if (Mathf.Abs(dx) < 1e-5f)
            {
                if (Mathf.Abs(x0) > laneHalf)
                {
                    return false;
                }
            }
            else
            {
                float ta = (-laneHalf - x0) / dx;
                float tb = (laneHalf - x0) / dx;
                t0 = Mathf.Max(t0, Mathf.Min(ta, tb));
                t1 = Mathf.Min(t1, Mathf.Max(ta, tb));

                if (t0 > t1)
                {
                    return false;
                }
            }

            za = Mathf.Lerp(z0, z1, t0);
            zb = Mathf.Lerp(z0, z1, t1);
            return true;
        }

        public bool Overlaps(Vector3 point, float radius)
        {
            Frame frame = CurrentFrame();
            Vector3 delta = point - frame.Center;
            delta.y = 0f;

            // 사각형에서 가장 가까운 점까지의 거리로 원과 겹치는지 본다.
            float x = Mathf.Max(Mathf.Abs(Vector3.Dot(delta, frame.Right)) - frame.Half.x, 0f);
            float z = Mathf.Max(Mathf.Abs(Vector3.Dot(delta, frame.Forward)) - frame.Half.y, 0f);
            return x * x + z * z <= radius * radius;
        }

        public void GetCorners(Vector3[] corners)
        {
            Frame frame = CurrentFrame();

            Vector3 r = frame.Right * frame.Half.x;
            Vector3 f = frame.Forward * frame.Half.y;

            // 둘레를 따라 도는 순서여야 인접한 두 점이 한 변이 된다.
            corners[0] = frame.Center + f + r;
            corners[1] = frame.Center - f + r;
            corners[2] = frame.Center - f - r;
            corners[3] = frame.Center + f - r;
        }

        /// <summary>차체를 direction 쪽으로 돌려 본 자세. 중심은 그대로 두고, 반폭·반길이는 차체 사각형을 그 축에 투영한 값이다.
        /// 차가 비스듬히 틀어져 있어도 그 방향으로 쓸고 지나갈 폭을 정확히 잡는다.</summary>
        private static Frame LaneFrame(Frame body, Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return body;
            }

            Vector3 forward = direction.normalized;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            float halfLength = Mathf.Abs(Vector3.Dot(body.Forward, forward)) * body.Half.y +
                               Mathf.Abs(Vector3.Dot(body.Right, forward)) * body.Half.x;
            float halfWidth = Mathf.Abs(Vector3.Dot(body.Forward, right)) * body.Half.y +
                              Mathf.Abs(Vector3.Dot(body.Right, right)) * body.Half.x;

            return new Frame
            {
                Center = body.Center,
                Forward = forward,
                Right = right,
                Half = new Vector2(halfWidth, halfLength),
            };
        }

        private Frame CurrentFrame()
        {
            Transform t = footprint != null ? footprint.transform : transform;

            Vector3 forward = t.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;

            return new Frame
            {
                Center = FootprintCenter(),
                Forward = forward,
                Right = new Vector3(forward.z, 0f, -forward.x),
                Half = HalfSize(),
            };
        }

        private Vector3 FootprintCenter()
        {
            return footprint != null
                ? footprint.transform.TransformPoint(footprint.center)
                : transform.position;
        }

        private Vector2 HalfSize()
        {
            if (footprint == null)
            {
                return fallbackHalfSize;
            }

            Vector3 scale = footprint.transform.lossyScale;

            if (footprint.shape != NavMeshObstacleShape.Box)
            {
                float r = footprint.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                return new Vector2(r, r);
            }

            Vector3 size = footprint.size;
            return new Vector2(size.x * 0.5f * Mathf.Abs(scale.x), size.z * 0.5f * Mathf.Abs(scale.z));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3[] corners = new Vector3[4];
            GetCorners(corners);

            Gizmos.color = ReferenceEquals(Blocker, null) ? new Color(0.3f, 1f, 0.5f, 0.8f) : new Color(1f, 0.4f, 0.3f, 0.8f);
            for (int i = 0; i < 4; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            }

            // 추월 중이면 목표점을 얼마나 옆으로 밀고 있는지 보여준다.
            if (_phase != BypassPhase.None)
            {
                Vector3 center = FootprintCenter();
                Gizmos.color = _phase == BypassPhase.Passing ? Color.cyan : Color.magenta;
                Gizmos.DrawLine(center, center + AvoidanceOffset);
            }
        }
#endif
    }
}
