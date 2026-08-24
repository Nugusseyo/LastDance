using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Data
{
    /// <summary>
    /// 차 한 대가 설 수 있는 자리. 씬에 놓고 MapData만 물려주면 스스로 등록한다.
    /// 자리를 늘리려면 이 오브젝트를 복제해 원하는 위치에 놓기만 하면 된다.
    /// </summary>
    public class ParkingSlot : MonoBehaviour
    {
        [Tooltip("이 자리를 등록할 맵 데이터. 같은 맵의 자리끼리는 같은 에셋을 물린다.")]
        [SerializeField] private MapDataSo mapData;

        [Header("기즈모")]
        [SerializeField] private Vector3 gizmoSize = new(2f, 0.1f, 4.5f);

        /// <summary>차가 정차할 위치.</summary>
        public Vector3 Position => transform.position;

        /// <summary>차가 바라볼 방향. 자리 오브젝트의 회전이 그대로 주차 방향이 된다.</summary>
        public Quaternion Rotation => transform.rotation;

        /// <summary>대여 중인지 여부. 상태를 바꾸는 것은 MapDataSo뿐이다.</summary>
        public bool IsOccupied { get; private set; }

        internal void SetOccupied(bool value)
        {
            IsOccupied = value;
        }

        private void OnEnable()
        {
            if (mapData == null)
            {
                Debug.LogError($"[ParkingSlot] {name}에 MapData를 지정해야 합니다.", this);
                return;
            }

            mapData.AddParkingSlot(this);
        }

        private void OnDisable()
        {
            if (mapData != null)
            {
                mapData.RemoveParkingSlot(this);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Application.isPlaying && IsOccupied
                ? new Color(1f, 0.4f, 0.3f, 0.5f)
                : new Color(0.3f, 1f, 0.5f, 0.5f);

            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, gizmoSize);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * (gizmoSize.z * 0.7f));
        }
    }
}
