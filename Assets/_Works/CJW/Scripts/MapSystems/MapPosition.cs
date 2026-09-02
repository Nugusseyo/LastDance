using UnityEngine;

namespace _Works.CJW.Scripts.MapSystems
{
    /// <summary>
    /// 맵 위의 한 지점. 씬에 놓고 MapData와 종류만 물려주면 스스로 등록한다.
    ///
    /// 좌표를 에셋에 굽지 않는 이유는 하나다. 이 게임은 런타임에 맵이 변한다.
    /// 가게를 넓히거나 가구를 옮기면 지점이 늘고 준다. 구워둔 좌표는 그 순간 거짓말이 된다.
    /// 여기서는 오브젝트가 켜지고 꺼질 때 목록이 따라 움직이므로 항상 씬이 진실이다.
    /// </summary>
    public class MapPosition : MonoBehaviour
    {
        [Tooltip("이 지점을 등록할 맵 데이터. 같은 맵의 지점끼리는 같은 에셋을 물린다.")]
        [SerializeField] private MapDataSo mapData;

        [Tooltip("이 지점의 종류. 쓰는 쪽은 이름이 아니라 이 값으로 찾는다.")]
        [SerializeField] private MapPointType type = MapPointType.None;

        [Header("기즈모")]
        [SerializeField] private Vector3 gizmoSize = new(1f, 0.1f, 1f);
        [SerializeField] private Color gizmoColor = new(0.3f, 1f, 0.5f, 0.5f);

        /// <summary>
        /// 이 지점의 종류. 종류가 고정된 파생 클래스는 이걸 덮어써서 인스펙터 설정을 없앨 수 있다.
        /// </summary>
        public virtual MapPointType Type => type;

        /// <summary>이 지점의 위치.</summary>
        public Vector3 Position => transform.position;

        /// <summary>이 지점이 가리키는 방향. 오브젝트의 회전이 그대로 쓰인다.</summary>
        public Quaternion Rotation => transform.rotation;

        /// <summary>지금 쓸 수 있는지. 대여 개념이 없는 지점은 항상 true다.</summary>
        public virtual bool IsAvailable => true;

        /// <summary>에디터 검사용. 어느 맵에 등록하려는지 인스펙터가 물어본다.</summary>
        public MapDataSo MapData => mapData;

        protected virtual void OnEnable()
        {
            if (mapData == null)
            {
                Debug.LogError($"[MapPosition] {name}에 MapData를 지정해야 합니다.", this);
                return;
            }

            if (Type == MapPointType.None)
            {
                Debug.LogError($"[MapPosition] {name}의 종류가 None입니다. MapPointType을 골라야 등록됩니다.", this);
                return;
            }

            mapData.Register(this);
        }

        protected virtual void OnDisable()
        {
            if (mapData != null)
            {
                mapData.Unregister(this);
            }
        }

        protected virtual Vector3 GetGizmoSize() => gizmoSize;

        protected virtual Color GetGizmoColor() => gizmoColor;

        protected virtual void OnDrawGizmos()
        {
            Vector3 size = GetGizmoSize();

            Gizmos.color = GetGizmoColor();
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * (size.z * 0.7f));
        }
    }
}
