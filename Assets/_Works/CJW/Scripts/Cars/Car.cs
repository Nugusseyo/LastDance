using System;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.CJW.Scripts.MapSystems;
using _Works.CJW.Scripts.Sounds;
using _Works.Shared.Cars;
using DevLib.ObjectPool.Runtime;
using Resources.DataBase.Human_Data;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Cars
{
    public abstract class Car : ManagingAgent, IPoolable, IRemovableCar
    {
        [field: SerializeField] public PoolItemSO PoolItem { get; set; }
        public GameObject GameObject => this != null ? gameObject : null;

        [Tooltip("좌석 위치. 비어 있는 칸은 좌석 수에서 자동으로 빠진다.")]
        [SerializeField] private Transform[] seats;
        [SerializeField] private Transform dropOffPoint;

        [Tooltip("정차 자리 방향으로 돌아설 때의 각속도(도/초).")]
        [SerializeField] private float parkingTurnSpeed = 180f;

        private ICarMoveModule _moveModule;


        /// <summary>seats에서 빈 칸을 걷어낸 실제 좌석. 좌석 수의 유일한 근거다.</summary>
        private Transform[] _usableSeats;

        /// <summary>이 차의 수치. 스폰될 때 <see cref="Setup"/>으로 주입된다.</summary>
        public CarDataSO Data { get; private set; }
        
        public HumanType HumanType { get; private set; }

        public void SetHumanType(HumanType type) => HumanType = type;

        /// <summary>실제로 쓸 수 있는 좌석 수. 배열 길이가 아니라 채워진 칸의 수다.</summary>
        public int SeatCount
        {
            get
            {
                EnsureSeatCache();
                return _usableSeats.Length;
            }
        }

        /// <summary>태울 인원 범위. 좌석 수가 곧 상한이다. 좌석이 없으면 (0, 0)이 나와 그 차는 방문을 시작하지 않는다.</summary>
        public Vector2Int CustomerCountRange
        {
            get
            {
                int seats = SeatCount;
                return seats <= 0 ? Vector2Int.zero : new Vector2Int(1, seats);
            }
        }

        /// <summary>손님을 한 명씩 태우고 내릴 때의 간격(초).</summary>
        public float BoardingInterval => Data != null ? Data.BoardingInterval : 0.4f;

        /// <summary>손님이 내려서 처음 서는 위치. 미지정이면 차량 위치.</summary>
        public Vector3 DropOffPosition => dropOffPoint != null ? dropOffPoint.position : transform.position;

        /// <summary>목적지까지 닿는 경로를 들고 있는지. 부분 경로면 기다려도 달라지지 않는다.</summary>
        public bool HasCompletePath => _moveModule == null || _moveModule.HasCompletePath;

        public bool IsArrived => _moveModule == null || _moveModule.IsArrived;

        /// <summary>소리를 내는 창구. 프리팹에 사운드 모듈이 없으면 null이고, 그 차는 소리 없이 달린다.</summary>
        public ISoundEmitter Sound { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            _moveModule = GetModule<ICarMoveModule>();
            Sound = GetModule<ISoundEmitter>();

            // 없으면 IsArrived가 항상 true라 이동 없이 모든 단계를 조용히 통과한다.
            // 증상이 "차가 스폰 자리에서 안 움직임"으로만 보이므로 반드시 남긴다.
            if (_moduleMissingLogged == false && _moveModule == null)
            {
                _moduleMissingLogged = true;
                Debug.LogError($"[Car] {name}에 ICarMoveModule이 없습니다. 이동 없이 방문이 즉시 끝납니다.", this);
            }

            EnsureSeatCache();
        }

        private bool _moduleMissingLogged;

        /// <summary>이 차를 치워 줄 쪽. 버려진 차일 때만 채워진다.</summary>
        private Func<Car, bool> _remover;

        public bool CanRemove => _remover != null;

        public bool Remove() => _remover != null && _remover(this);

        /// <summary>버려진 차로 등록한 쪽(<see cref="Customers.Visit.VisitDirector"/>)이 치우는 방법을 맡긴다. null이면 더는 치울 수 없다.</summary>
        public void SetRemover(Func<Car, bool> remover) => _remover = remover;

        /// <summary>풀에서 꺼낸 직후 이 차가 쓸 데이터를 넣어준다.</summary>
        public virtual void Setup(CarDataSO data)
        {
            Data = data;
            if (data == null)
            {
                return;
            }

            _moveModule ??= GetModule<ICarMoveModule>();
            _moveModule?.ApplyStats(data.MoveSpeed, data.ArriveThreshold);
        
        }

        /// <summary>인덱스가 좌석 범위 안인지. 태우기 전에 물어보면 경고 없이 확인할 수 있다.</summary>
        public bool HasSeat(int index) => index >= 0 && index < SeatCount;

        public Transform GetSeat(int index)
        {
            EnsureSeatCache();

            if (index < 0 || index >= _usableSeats.Length)
            {
                Debug.LogWarning($"[Car] {name}에 {index}번 좌석이 없습니다. (좌석 수: {_usableSeats.Length})", this);
                return transform;
            }

            return _usableSeats[index];
        }

        public void MoveTo(Vector3 destination)
        {
            _moveModule?.MoveTo(destination);
        }

        /// <summary>진입점을 거쳐 목적지로 들어간다. 마지막 구간이 직선이라 도착 방향이 거의 맞추어진다.</summary>
        public void MoveTo(Vector3 destination, Vector3 approachFrom)
        {
            _moveModule?.MoveTo(destination, approachFrom);
        }

        public void Stop()
        {
            _moveModule?.Stop();
        }

        /// <summary>지금 목적지로 가기 전에 먼저 곧게 물러선다. MoveTo 뒤에 부른다.</summary>
        public void BackOff(float distance)
        {
            _moveModule?.BackOff(distance);
        }

        /// <summary>지금 목적지로 가기 전에 제자리에서 앞뒤로 오가며 이 방향으로 튼다. MoveTo 뒤에 부른다.</summary>
        public void TurnInPlace(Vector3 direction)
        {
            _moveModule?.TurnInPlace(direction);
        }

        /// <summary>제자리 회전 같은 기동 중인지. 이 동안은 앞으로 나아가지 않아도 막힌 게 아니다.</summary>
        public bool IsManeuvering => _moveModule != null && _moveModule.IsManeuvering;

        /// <summary>정차 자리 방향으로 조금씩 돌린다. 회전이 다 맞으면 true. Stop() 뒤에 불러야 한다.</summary>
        public bool AlignTo(Quaternion target, float dt)
        {
            float step = Mathf.Max(parkingTurnSpeed, 1f) * dt;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, step);

            return Quaternion.Angle(transform.rotation, target) <= 0.1f;
        }


        public virtual void ResetItem()
        {
            // 다음 스폰에서 Setup이 다시 넣어준다. 남겨두면 이전 방문의 값이 샌다.
            Data = null;
            HumanType = Resources.DataBase.Human_Data.HumanType.None;
            _remover = null;
            Stop();
            GetModule<ICarWheelModule>()?.ResetPose();

            // 지나가던 차가 다음에 방문 차로 나올 수 있다. 에이전트·구멍을 꺼 둔 채 두면 길을 찾지 못한다.
            SetGliding(false);
        }

        private NavMeshAgent _agent;
        private NavMeshObstacle _footprint;

        /// <summary>도로를 지나가기만 하는 차처럼 밖에서 직접 옮길지(<see cref="Glide"/>). 켜면 경로 찾기와 조향을 멈추고,
        /// 에이전트와 NavMesh 구멍(장애물)을 끈다 — 달리는 차가 구멍을 내면 도로 NavMesh가 매 프레임 다시 깎여 다른 차의 길이 흔들린다.
        /// 교통 센서는 그대로 둬서 방문 차가 이 차를 보고 양보한다.</summary>
        public void SetGliding(bool gliding)
        {
            if (gliding)
            {
                Stop();
            }

            _agent ??= GetComponentInChildren<NavMeshAgent>(true);
            _footprint ??= GetComponentInChildren<NavMeshObstacle>(true);

            if (_agent != null)
            {
                _agent.enabled = !gliding;
            }

            if (_footprint != null)
            {
                _footprint.enabled = !gliding;
            }
        }

        /// <summary><see cref="SetGliding"/>을 켠 차를 이 자리로 옮긴다. 바닥 높이는 이동 모듈이 맞춘다.</summary>
        public void Glide(Vector3 position, Quaternion rotation, float speed)
        {
            if (_moveModule == null || !_moveModule.Glide(position, rotation, speed))
            {
                transform.SetPositionAndRotation(position, rotation);
            }
        }

        private void EnsureSeatCache()
        {
            if (_usableSeats != null)
            {
                return;
            }

            if (seats == null || seats.Length == 0)
            {
                _usableSeats = Array.Empty<Transform>();
                return;
            }

            int count = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] != null)
                {
                    count++;
                }
            }

            _usableSeats = new Transform[count];

            int cursor = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] != null)
                {
                    _usableSeats[cursor++] = seats[i];
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 인스펙터에서 배열을 건드렸을 수 있으므로 캐시를 버린다.
            _usableSeats = null;

            if (seats == null || seats.Length == 0)
            {
                return;
            }

            // 채우는 중에는 빈 칸이 당연히 생기므로, 배열을 다 채운 뒤에만 중복을 따진다.
            int filled = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] != null)
                {
                    filled++;
                }
            }

            if (filled < seats.Length)
            {
                return;
            }

            for (int i = 0; i < seats.Length; i++)
            {
                for (int j = i + 1; j < seats.Length; j++)
                {
                    if (seats[i] == seats[j])
                    {
                        Debug.LogWarning($"[Car] {name}의 Seats {i}번과 {j}번이 같은 Transform입니다. 손님이 겹쳐 앉습니다.", this);
                    }
                }
            }
        }
#endif
    }
}
