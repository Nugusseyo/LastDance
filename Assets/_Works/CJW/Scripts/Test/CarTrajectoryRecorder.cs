using System.Collections.Generic;
using System.Text;
using _Works.CJW.Scripts.Customers.Cars;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.CJW.Scripts.Test
{
    /// <summary>
    /// 차량 궤적을 일정 간격으로 기록하는 디버그용 컴포넌트. 튜닝이 끝나면 지워도 된다.
    /// 씬에 빈 오브젝트를 만들어 붙이거나, 런타임에 AddComponent로 붙이면 된다.
    /// </summary>
    public class CarTrajectoryRecorder : MonoBehaviour
    {
        public struct Sample
        {
            public float Time;
            public int CarId;
            public Vector3 Position;
            public float Heading;
            public float Speed;
            public float Steer;
            public Vector3 Destination;
            public bool HasPath;
        }

        [Tooltip("샘플 간격(초). 0이면 매 프레임.")]
        [SerializeField] private float interval = 0.1f;

        [Tooltip("보관할 최대 샘플 수. 넘으면 오래된 것부터 버린다.")]
        [SerializeField] private int maxSamples = 6000;

        /// <summary>도메인 리로드 전까지 유지되는 기록. 외부에서 execute_code로 읽어간다.</summary>
        public static readonly List<Sample> Samples = new List<Sample>();

        private float _timer;

        private void Awake()
        {
            // 에디터 창이 비활성이어도 계속 돌아야 궤적이 끊기지 않는다.
            Application.runInBackground = true;
            Samples.Clear();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f)
            {
                return;
            }

            _timer = interval;
            Capture();
        }

        private void Capture()
        {
            Car[] cars = FindObjectsOfType<Car>();

            for (int i = 0; i < cars.Length; i++)
            {
                Car car = cars[i];
                if (car == null || !car.gameObject.activeInHierarchy)
                {
                    continue;
                }

                CarSteeringMoveModule move = car.GetComponent<CarSteeringMoveModule>();
                NavMeshAgent agent = car.GetComponent<NavMeshAgent>();

                Vector3 forward = car.transform.forward;

                Samples.Add(new Sample
                {
                    Time = Time.time,
                    CarId = car.GetInstanceID(),
                    Position = car.transform.position,
                    Heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg,
                    Speed = move != null ? move.Speed : 0f,
                    Steer = move != null ? move.SteerAngleDeg : 0f,
                    Destination = agent != null && agent.hasPath ? agent.destination : Vector3.zero,
                    HasPath = agent != null && agent.hasPath
                });
            }

            if (Samples.Count > maxSamples)
            {
                Samples.RemoveRange(0, Samples.Count - maxSamples);
            }
        }

        /// <summary>기록을 CSV로 뽑는다. carId가 0이면 전부, 아니면 그 차만.</summary>
        public static string Dump(int carId = 0, float fromTime = 0f, float toTime = float.MaxValue, int stride = 1)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("t,car,x,z,hdg,v,steer\n");

            int written = 0;
            for (int i = 0; i < Samples.Count; i++)
            {
                Sample s = Samples[i];

                if (carId != 0 && s.CarId != carId)
                {
                    continue;
                }

                if (s.Time < fromTime || s.Time > toTime)
                {
                    continue;
                }

                if (stride > 1 && written % stride != 0)
                {
                    written++;
                    continue;
                }

                written++;

                sb.Append(s.Time.ToString("F2")).Append(',')
                  .Append(s.CarId).Append(',')
                  .Append(s.Position.x.ToString("F2")).Append(',')
                  .Append(s.Position.z.ToString("F2")).Append(',')
                  .Append(s.Heading.ToString("F0")).Append(',')
                  .Append(s.Speed.ToString("F2")).Append(',')
                  .Append(s.Steer.ToString("F0")).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>차별로 누적 회전량과 이동 거리를 요약한다. 쓸데없이 도는 구간을 찾는 용도.</summary>
        public static string Summary()
        {
            Dictionary<int, float> turned = new Dictionary<int, float>();
            Dictionary<int, float> traveled = new Dictionary<int, float>();
            Dictionary<int, Vector3> lastPos = new Dictionary<int, Vector3>();
            Dictionary<int, float> lastHdg = new Dictionary<int, float>();
            Dictionary<int, float> firstTime = new Dictionary<int, float>();
            Dictionary<int, float> lastTime = new Dictionary<int, float>();

            for (int i = 0; i < Samples.Count; i++)
            {
                Sample s = Samples[i];

                if (!lastPos.ContainsKey(s.CarId))
                {
                    turned[s.CarId] = 0f;
                    traveled[s.CarId] = 0f;
                    firstTime[s.CarId] = s.Time;
                }
                else
                {
                    turned[s.CarId] += Mathf.Abs(Mathf.DeltaAngle(lastHdg[s.CarId], s.Heading));
                    traveled[s.CarId] += Vector3.Distance(lastPos[s.CarId], s.Position);
                }

                lastPos[s.CarId] = s.Position;
                lastHdg[s.CarId] = s.Heading;
                lastTime[s.CarId] = s.Time;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("car,seconds,distance,totalTurnDeg\n");

            foreach (KeyValuePair<int, float> pair in turned)
            {
                sb.Append(pair.Key).Append(',')
                  .Append((lastTime[pair.Key] - firstTime[pair.Key]).ToString("F1")).Append(',')
                  .Append(traveled[pair.Key].ToString("F1")).Append(',')
                  .Append(pair.Value.ToString("F0")).Append('\n');
            }

            return sb.ToString();
        }
    }
}
