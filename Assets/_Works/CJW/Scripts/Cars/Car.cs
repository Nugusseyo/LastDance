using System;
using _Works.CJW.Scripts.Customers;
using _Works.CJW.Scripts.Customers.Data;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.CJW.Scripts.MapSystems;
using DevLib.ObjectPool.Runtime;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    public abstract class Car : ManagingAgent, IPoolable
    {
        [field: SerializeField] public PoolItemSO PoolItem { get; set; }
        public GameObject GameObject => this != null ? gameObject : null;

        [Tooltip("좌석 위치. 비어 있는 칸은 좌석 수에서 자동으로 빠진다.")]
        [SerializeField] private Transform[] seats;
        [SerializeField] private Transform dropOffPoint;

        [Tooltip("정차 자리 방향으로 돌아설 때의 각속도(도/초).")]
        [SerializeField] private float parkingTurnSpeed = 180f;

        [Tooltip("차체 색을 덮어쓸 렌더러. 비워두면 색 변형을 쓰지 않는다.")]
        [SerializeField] private Renderer[] bodyRenderers;

        private ICarMoveModule _moveModule;

        /// <summary>URP Lit의 색 프로퍼티. 커스텀 셰이더를 쓰면 이 이름을 맞춰야 한다.</summary>
        private static readonly int BodyColorId = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock _mpb;

        /// <summary>seats에서 빈 칸을 걷어낸 실제 좌석. 좌석 수의 유일한 근거다.</summary>
        private Transform[] _usableSeats;

        /// <summary>이 차의 수치. 스폰될 때 <see cref="Setup"/>으로 주입된다.</summary>
        public CarDataSO Data { get; private set; }

        /// <summary>실제로 쓸 수 있는 좌석 수. 배열 길이가 아니라 채워진 칸의 수다.</summary>
        public int SeatCount
        {
            get
            {
                EnsureSeatCache();
                return _usableSeats.Length;
            }
        }

        /// <summary>
        /// 태울 인원 범위. 좌석 수가 곧 상한이라 어긋날 값이 애초에 없다.
        /// 좌석이 하나도 없으면 (0, 0)이 나오고, 그 차는 방문을 시작하지 않는다.
        /// </summary>
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

        public bool IsArrived => _moveModule == null || _moveModule.IsArrived;

        protected override void Awake()
        {
            base.Awake();
            _moveModule = GetModule<ICarMoveModule>();
            EnsureSeatCache();
        }

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

            ApplyBodyColor(data.BodyColors);
        }

        /// <summary>
        /// 후보 중 하나를 골라 차체 색만 갈아끼운다. MaterialPropertyBlock이라
        /// 머티리얼 인스턴스가 생기지 않고 SRP Batcher도 그대로 묶인다.
        /// </summary>
        private void ApplyBodyColor(Color[] candidates)
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0)
            {
                return;
            }

            if (candidates == null)
            {
                // 색을 지정하지 않은 데이터. 이전 방문의 색이 남지 않게 프리팹 색으로 되돌린다.
                ClearBodyColor();
                return;
            }

            Color color = candidates[UnityEngine.Random.Range(0, candidates.Length)];
            _mpb ??= new MaterialPropertyBlock();

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Renderer renderer = bodyRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                // 다른 곳에서 걸어둔 오버라이드를 지우지 않도록 기존 블록을 읽어와서 색만 덮는다.
                renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BodyColorId, color);
                renderer.SetPropertyBlock(_mpb);
            }
        }

        private void ClearBodyColor()
        {
            if (bodyRenderers == null)
            {
                return;
            }

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                {
                    bodyRenderers[i].SetPropertyBlock(null);
                }
            }
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

        /// <summary>
        /// 정차 자리 방향으로 조금씩 돌린다. 회전이 다 맞으면 true.
        /// NavMesh가 회전을 다시 가져가지 않도록 Stop() 뒤에 불러야 한다.
        /// </summary>
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
            Stop();
            ClearBodyColor();
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
