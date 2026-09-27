using System.Collections.Generic;
using _Works.CJW.Scripts.ManagingAgents;
using _Works.CJW.Scripts.MapSystems;
using DevLib.EventChannelSystem;
using DevLib.SoundSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>차고 앞에 미리 세워 둔, 손님이 훔쳐 타고 달아날 수 있는 차. 씬에 놓으면 스스로 목록에 오른다.
    /// 풀에서 꺼낸 차가 아니라서 방문 흐름을 타지 않는다 — 훔친 손님이 태워지면 스스로 퇴장 지점까지 달리고 사라진다.</summary>
    [RequireComponent(typeof(Car))]
    public class StealableCar : MonoBehaviour
    {
        [Tooltip("훔쳐 달릴 때만 틱 등록을 요청할 채널. 서 있는 동안에는 틱이 필요 없다.")]
        [SerializeField] private EventChannelSO agentChannel;

        [Tooltip("달릴 때 쓸 속도 같은 수치.")]
        [SerializeField] private CarDataSO carData;

        [Tooltip("훔친 뒤 달아날 곳. 손님 차의 퇴장 지점과 같은 곳을 물린다.")]
        [SerializeField] private Transform exitPoint;

        [Tooltip("퇴장 지점에서 이 반경(m) 안에 들어오면 다 빠져나간 것으로 본다.")]
        [SerializeField, Min(0f)] private float exitRadius = 6f;

        [Header("사운드")]
        [Tooltip("훔쳐 달아나기 시작할 때 낼 소리(시동과 타이어 끌리는 급출발 소리). 차에 사운드 모듈이 있어야 난다.")]
        [SerializeField] private SoundClipSo driveAwaySound;

        private static readonly List<StealableCar> Registered = new();

        /// <summary>지금 씬에 서 있는 훔칠 수 있는 차. 이미 찜했거나 훔쳐 간 차도 들어 있으니 <see cref="IsClaimed"/>로 거른다.</summary>
        public static IReadOnlyList<StealableCar> All => Registered;

        public Car Car { get; private set; }

        /// <summary>누군가 훔치러 가는 중이거나 이미 훔쳐 갔는지. 한 대를 두 손님이 노리지 않게 한다.</summary>
        public bool IsClaimed { get; private set; }

        /// <summary>손님이 다가가 탈 곳. 차가 정한 하차 지점과 같다.</summary>
        public Vector3 DoorPosition => Car.DropOffPosition;

        /// <summary>훔친 손님이 앉을 자리. 0번 좌석이 없으면 차 원점이다.</summary>
        public Transform DriverSeat => Car.HasSeat(0) ? Car.GetSeat(0) : Car.transform;

        /// <summary>퇴장 지점에 닿았는지. 달리기 시작하기 전에는 늘 false다.</summary>
        public bool HasEscaped
        {
            get
            {
                if (!_driving || exitPoint == null)
                {
                    return false;
                }

                Vector3 delta = exitPoint.position - transform.position;
                delta.y = 0f;
                return Car.IsArrived || delta.sqrMagnitude <= exitRadius * exitRadius;
            }
        }

        private bool _driving;

        private void Awake()
        {
            Car = GetComponent<Car>();
        }

        private void OnEnable()
        {
            Registered.Add(this);
        }

        private void OnDisable()
        {
            Registered.Remove(this);
        }

        /// <summary>훔치러 가기 전에 찜한다. 이미 누가 찜했으면 false.</summary>
        public bool TryClaim()
        {
            if (IsClaimed)
            {
                return false;
            }

            IsClaimed = true;
            return true;
        }

        /// <summary>훔치러 가다 포기했을 때 찜을 푼다. 달리기 시작한 차는 되돌릴 수 없으므로 풀지 않는다.</summary>
        public void ReleaseClaim()
        {
            if (!_driving)
            {
                IsClaimed = false;
            }
        }

        /// <summary>손님을 태운 뒤 부른다. 퇴장 지점으로 달리기 시작한다.</summary>
        public void DriveAway()
        {
            if (_driving)
            {
                return;
            }

            if (exitPoint == null)
            {
                Debug.LogError($"[StealableCar] {name}에 퇴장 지점을 지정해야 달아날 수 있습니다.", this);
                return;
            }

            _driving = true;
            Car.Setup(carData);
            if (agentChannel != null)
            {
                agentChannel.RaiseEvent(AgentEvents.RegisterAgentEvent.Init(Car));
            }

            Car.MoveTo(exitPoint.position);
            Car.Sound?.Play(driveAwaySound);
        }

        /// <summary>달아난 차를 씬에서 치운다. 타고 있던 손님은 먼저 풀로 돌려보낸 뒤에 불러야 함께 꺼지지 않는다.</summary>
        public void Vanish()
        {
            if (_driving && agentChannel != null)
            {
                agentChannel.RaiseEvent(AgentEvents.UnRegisterAgentEvent.Init(Car));
            }

            gameObject.SetActive(false);
        }

        // 도메인 리로드를 끈 에디터에서는 static이 플레이 사이에 남는다. 이전 플레이의 차가 섞이지 않게 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Registered.Clear();
    }
}
