using System;
using System.Collections.Generic;
using _Works.CJW.Scripts.Cars;
using UnityEngine;

namespace _Works.CJW.Scripts.Title
{
    /// <summary>타이틀 배경에서 도로를 끝없이 지나다니는 차들.
    /// 게임용 차 프리팹의 겉모습(메시·바퀴)만 복사해 쓰므로 손님·NavMesh·풀 같은 게임 시스템 없이 혼자 돈다.
    /// 차선마다 시작점→끝점 직선을 따라 달리고, 같은 차선 앞차가 가까우면 속도만 줄여 따라간다.</summary>
    public class TitleTraffic : MonoBehaviour
    {
        [Serializable]
        public class Lane
        {
            [Tooltip("차가 나타나는 지점. 카메라에 안 보이는 곳에 둔다.")]
            public Transform from;

            [Tooltip("차가 사라지는 지점. 카메라에 안 보이는 곳에 둔다.")]
            public Transform to;
        }

        private sealed class WheelView
        {
            public Transform Transform;
            public Vector3 SpinAxis;
            public Quaternion BaseRotation;
            public float Radius;
            public float Angle;
        }

        private sealed class Driving
        {
            public GameObject Go;
            public int Template;
            public WheelView[] Wheels;
            public float Length;
            public float Distance;
            public float Speed;
            public float MaxSpeed;
            public float Height;

            /// <summary>루트에서 차 바닥(바퀴 아래)까지의 높이. 모델마다 피벗이 달라 도로면에 이만큼 올려 놓는다.</summary>
            public float GroundOffset;
        }

        private sealed class Template
        {
            public GameObject Go;

            /// <summary>복사본 루트 기준 바퀴 경로와 굴리는 축.</summary>
            public List<(string path, Vector3 axis, float radius)> Wheels = new();
        }

        [Tooltip("겉모습을 빌릴 차 프리팹들. 메시와 바퀴 설정만 쓰고 스크립트는 복사하지 않는다.")]
        [SerializeField] private GameObject[] carPrefabs = Array.Empty<GameObject>();

        [SerializeField] private Lane[] lanes = Array.Empty<Lane>();

        [Tooltip("차가 새로 나오는 간격(초). 차선은 매번 무작위로 고른다.")]
        [SerializeField] private Vector2 spawnInterval = new(1.2f, 3f);

        [Tooltip("차마다 뽑는 최고 속도(m/s).")]
        [SerializeField] private Vector2 speedRange = new(9f, 15f);

        [Tooltip("앞차 범퍼와 둘 최소 간격(m).")]
        [SerializeField, Min(0.5f)] private float minGap = 3f;

        [Tooltip("출발·따라잡을 때의 가속(m/s²).")]
        [SerializeField, Min(0.1f)] private float accel = 4f;

        [Tooltip("앞차 때문에 줄일 때 쓰는 감속(m/s²).")]
        [SerializeField, Min(0.1f)] private float brake = 6f;

        [Tooltip("시작할 때 도로가 빈 채로 보이지 않게 차선에 차를 미리 깔아 둔다.")]
        [SerializeField] private bool prewarm = true;

        [Tooltip("차 높이를 도로 윗면에 맞출 때 아래로 쏘는 레이의 대상.")]
        [SerializeField] private LayerMask groundMask = ~0;

        private readonly List<Template> _templates = new();
        private readonly Dictionary<int, Stack<Driving>> _pool = new();
        private List<Driving>[] _cars;
        private Transform _templateRoot;
        private float _spawnTimer;

        private void Awake()
        {
            BuildTemplates();

            _cars = new List<Driving>[lanes.Length];
            for (int i = 0; i < _cars.Length; i++)
            {
                _cars[i] = new List<Driving>();
            }
        }

        private void Start()
        {
            if (_templates.Count == 0 || lanes.Length == 0)
            {
                Debug.LogWarning("[TitleTraffic] 차 프리팹이나 차선이 없어 차를 띄우지 않습니다.", this);
                enabled = false;
                return;
            }

            if (prewarm)
            {
                Prewarm();
            }

            _spawnTimer = UnityEngine.Random.Range(spawnInterval.x, spawnInterval.y);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = UnityEngine.Random.Range(spawnInterval.x, spawnInterval.y);
                TrySpawn(UnityEngine.Random.Range(0, lanes.Length), 0f);
            }

            for (int i = 0; i < lanes.Length; i++)
            {
                TickLane(i, dt);
            }
        }

        private void TickLane(int laneIndex, float dt)
        {
            if (!TryGetLine(laneIndex, out Vector3 from, out Vector3 dir, out float length))
            {
                return;
            }

            List<Driving> cars = _cars[laneIndex];
            Quaternion facing = Quaternion.LookRotation(dir, Vector3.up);

            // 앞차(멀리 간 차)부터 본다. 뒤차는 이미 옮긴 앞차 위치를 기준으로 속도를 정한다.
            for (int i = 0; i < cars.Count; i++)
            {
                Driving car = cars[i];
                float target = car.MaxSpeed;

                if (i > 0)
                {
                    Driving ahead = cars[i - 1];
                    float gap = ahead.Distance - car.Distance - (ahead.Length + car.Length) * 0.5f - minGap;
                    // 이 감속으로 간격 안에 설 수 있는 속도 + 앞차 속도까지만 낸다.
                    float safe = Mathf.Sqrt(2f * brake * Mathf.Max(0f, gap)) + ahead.Speed * 0.5f;
                    target = Mathf.Min(target, gap <= 0f ? 0f : safe);
                }

                car.Speed = target < car.Speed
                    ? Mathf.Max(target, car.Speed - brake * 2f * dt)
                    : Mathf.MoveTowards(car.Speed, target, accel * dt);
                car.Distance += car.Speed * dt;

                Place(car, from, dir, facing, dt);
                SpinWheels(car, dt);
            }

            // 끝까지 간 차는 맨 앞에 있다.
            while (cars.Count > 0 && cars[0].Distance >= length)
            {
                Release(cars[0]);
                cars.RemoveAt(0);
            }
        }

        private void Prewarm()
        {
            for (int lane = 0; lane < lanes.Length; lane++)
            {
                if (!TryGetLine(lane, out _, out _, out float length))
                {
                    continue;
                }

                // 뒤(시작점 쪽)에서 앞으로 깔면 리스트 순서가 뒤집히니, 먼 쪽부터 깐다.
                float d = length - UnityEngine.Random.Range(5f, 20f);
                while (d > 10f)
                {
                    TrySpawn(lane, d);
                    d -= UnityEngine.Random.Range(18f, 45f);
                }
            }
        }

        private void TrySpawn(int laneIndex, float distance)
        {
            if (!TryGetLine(laneIndex, out Vector3 from, out Vector3 dir, out _))
            {
                return;
            }

            List<Driving> cars = _cars[laneIndex];
            if (cars.Count > 0)
            {
                Driving last = cars[cars.Count - 1];
                if (last.Distance - distance < last.Length + minGap + 4f)
                {
                    return;
                }
            }

            Driving car = Take(UnityEngine.Random.Range(0, _templates.Count));
            car.Distance = distance;
            car.MaxSpeed = UnityEngine.Random.Range(speedRange.x, speedRange.y);
            car.Speed = car.MaxSpeed;

            // 뒤에서 오는 차가 앞차보다 빠르면 곧장 붙으니 앞차 속도로 시작한다.
            if (cars.Count > 0)
            {
                car.Speed = Mathf.Min(car.Speed, cars[cars.Count - 1].Speed);
            }

            car.Height = from.y + car.GroundOffset;
            Place(car, from, dir, Quaternion.LookRotation(dir, Vector3.up), 0f);
            cars.Add(car);
        }

        private void Place(Driving car, Vector3 from, Vector3 dir, Quaternion facing, float dt)
        {
            Vector3 pos = from + dir * car.Distance;

            if (Physics.Raycast(pos + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 12f, groundMask, QueryTriggerInteraction.Ignore))
            {
                float ground = hit.point.y + car.GroundOffset;
                car.Height = dt > 0f ? Mathf.Lerp(car.Height, ground, 1f - Mathf.Exp(-12f * dt)) : ground;
            }

            pos.y = car.Height;
            car.Go.transform.SetPositionAndRotation(pos, facing);
        }

        private static void SpinWheels(Driving car, float dt)
        {
            foreach (WheelView wheel in car.Wheels)
            {
                // 굴러간 거리 / 반지름 = 돈 각도(라디안).
                wheel.Angle = Mathf.Repeat(wheel.Angle + car.Speed * dt / wheel.Radius * Mathf.Rad2Deg, 360f);
                wheel.Transform.localRotation = wheel.BaseRotation * Quaternion.AngleAxis(wheel.Angle, wheel.SpinAxis);
            }
        }

        private bool TryGetLine(int laneIndex, out Vector3 from, out Vector3 dir, out float length)
        {
            Lane lane = lanes[laneIndex];
            from = default;
            dir = default;
            length = 0f;

            if (lane?.from == null || lane.to == null)
            {
                return false;
            }

            from = lane.from.position;
            Vector3 line = lane.to.position - from;
            line.y = 0f;
            length = line.magnitude;
            if (length < 1f)
            {
                return false;
            }

            dir = line / length;
            return true;
        }

        private Driving Take(int templateIndex)
        {
            if (_pool.TryGetValue(templateIndex, out Stack<Driving> stack) && stack.Count > 0)
            {
                Driving pooled = stack.Pop();
                pooled.Go.SetActive(true);
                return pooled;
            }

            Template template = _templates[templateIndex];
            GameObject go = Instantiate(template.Go, transform);
            go.name = template.Go.name;
            go.SetActive(true);

            var wheels = new List<WheelView>();
            foreach ((string path, Vector3 axis, float radius) in template.Wheels)
            {
                Transform t = go.transform.Find(path);
                if (t == null)
                {
                    continue;
                }

                wheels.Add(new WheelView
                {
                    Transform = t,
                    SpinAxis = axis,
                    BaseRotation = t.localRotation,
                    Radius = radius > 0f ? radius : MeasureRadius(t),
                });
            }

            Bounds bounds = MeasureBounds(go);
            return new Driving
            {
                Go = go,
                Template = templateIndex,
                Wheels = wheels.ToArray(),
                Length = Mathf.Max(2f, bounds.size.z),
                GroundOffset = go.transform.position.y - bounds.min.y,
            };
        }

        private void Release(Driving car)
        {
            car.Go.SetActive(false);

            if (!_pool.TryGetValue(car.Template, out Stack<Driving> stack))
            {
                stack = new Stack<Driving>();
                _pool[car.Template] = stack;
            }

            stack.Push(car);
        }

        /// <summary>프리팹마다 스크립트 없이 메시만 옮긴 복사본을 꺼진 보관함에 만들어 둔다.</summary>
        private void BuildTemplates()
        {
            _templateRoot = new GameObject("Templates").transform;
            _templateRoot.SetParent(transform, false);
            _templateRoot.gameObject.SetActive(false);

            foreach (GameObject prefab in carPrefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                var template = new Template { Go = CopyVisual(prefab.transform, _templateRoot, true) };
                if (template.Go == null)
                {
                    continue;
                }

                CarWheelModule wheelModule = prefab.GetComponentInChildren<CarWheelModule>(true);
                if (wheelModule != null)
                {
                    foreach (CarWheelModule.Wheel wheel in wheelModule.Wheels)
                    {
                        if (wheel?.transform != null)
                        {
                            template.Wheels.Add((PathFrom(prefab.transform, wheel.transform), wheel.spinAxis, wheel.radius));
                        }
                    }
                }

                _templates.Add(template);
            }
        }

        /// <summary>Transform 계층과 MeshFilter·MeshRenderer만 복사한다. 렌더러가 하나도 없는 가지(충돌체 등)는 건너뛴다.</summary>
        private static GameObject CopyVisual(Transform src, Transform parent, bool isRoot = false)
        {
            if (!src.gameObject.activeSelf || src.GetComponentInChildren<MeshRenderer>() == null)
            {
                return null;
            }

            var go = new GameObject(src.name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            // 루트는 원점·회전 없이 둔다. 위치·방향은 달릴 때 정한다.
            t.localPosition = isRoot ? Vector3.zero : src.localPosition;
            t.localRotation = isRoot ? Quaternion.identity : src.localRotation;
            t.localScale = src.localScale;

            if (src.TryGetComponent(out MeshFilter filter) && src.TryGetComponent(out MeshRenderer renderer) && renderer.enabled)
            {
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer copy = go.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = renderer.shadowCastingMode;
                copy.receiveShadows = renderer.receiveShadows;
            }

            foreach (Transform child in src)
            {
                CopyVisual(child, t);
            }

            return go;
        }

        private static string PathFrom(Transform root, Transform target)
        {
            string path = target.name;
            for (Transform p = target.parent; p != null && p != root; p = p.parent)
            {
                path = p.name + "/" + path;
            }

            return path;
        }

        /// <summary>차 전체 렌더러 범위. 생성 직후 루트가 회전 없는 상태라 z 크기가 곧 앞뒤 길이, 최저점이 바퀴 바닥이다.</summary>
        private static Bounds MeasureBounds(GameObject go)
        {
            go.transform.rotation = Quaternion.identity;
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(go.transform.position, new Vector3(2f, 1.5f, 4.5f));
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }

            return b;
        }

        private static float MeasureRadius(Transform wheel)
        {
            Renderer r = wheel.GetComponentInChildren<Renderer>();
            return r != null && r.bounds.extents.y > 0.01f ? r.bounds.extents.y : 0.35f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            foreach (Lane lane in lanes)
            {
                if (lane?.from != null && lane.to != null)
                {
                    Gizmos.DrawLine(lane.from.position, lane.to.position);
                    Gizmos.DrawSphere(lane.from.position, 0.5f);
                }
            }
        }
    }
}
