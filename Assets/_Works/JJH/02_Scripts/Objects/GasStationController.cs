using _Works.JJH._02_Scripts.Items;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Objects
{
    public class GasStationController : MonoBehaviour
    {
        [Header("Nozzle")]
        [SerializeField] private FuelNozzle nozzlePrefab;
        [SerializeField] private Transform nozzleSocket;
        [SerializeField] private float pickupDistance = 3f;

        [Header("Visual")]
        [SerializeField] private GameObject nozzleAttachedVisual;
        [SerializeField] private GameObject nozzleDetachedVisual;

        [Header("Hose")]
        [SerializeField] private LineRenderer hoseLine;
        [SerializeField] private Transform hoseStartPoint;
        [SerializeField] private int hoseSegments = 12;
        [SerializeField] private float hoseSlack = 0.4f;

        [Header("Hose Snap")]
        [SerializeField] private float maxHoseLength = 10f;
        [SerializeField, Range(0.5f, 1f)] private float tensionStartRatio = 0.7f;

        public bool IsDetached { get; private set; }

        private FuelNozzle _activeNozzle;

        private void Awake()
        {
            SetVisual(true);
            SetHoseActive(false);
        }

        private void LateUpdate()
        {
            if (!IsDetached)
                return;

            if (_activeNozzle == null)
            {
                ResetStation();
                return;
            }

            if (GetHoseStart().Equals(default) == false &&
                Vector3.Distance(GetHoseStart(), _activeNozzle.transform.position) > maxHoseLength)
            {
                SnapHose();
                return;
            }

            if (hoseLine != null)
                UpdateHose();
        }

        public FuelNozzle TryDetachNozzle()
        {
            if (IsDetached || nozzlePrefab == null || nozzleSocket == null)
                return null;

            FuelNozzle instance = Instantiate(nozzlePrefab, nozzleSocket.position, nozzleSocket.rotation);
            instance.SetStation(this);

            _activeNozzle = instance;
            IsDetached = true;

            SetVisual(false);
            SetHoseActive(true);

            return instance;
        }

        public bool TryAttachNozzle(FuelNozzle targetNozzle)
        {
            if (targetNozzle == null || targetNozzle != _activeNozzle || !IsDetached)
                return false;

            Destroy(targetNozzle.gameObject);
            ResetStation();

            return true;
        }

        private void SnapHose()
        {
            if (_activeNozzle != null)
                Destroy(_activeNozzle.gameObject);

            ResetStation();
        }

        private void ResetStation()
        {
            _activeNozzle = null;
            IsDetached = false;

            SetVisual(true);
            SetHoseActive(false);
        }

        private Vector3 GetHoseStart()
        {
            return hoseStartPoint != null ? hoseStartPoint.position : transform.position;
        }

        private void SetVisual(bool attached)
        {
            if (nozzleAttachedVisual != null)
                nozzleAttachedVisual.SetActive(attached);

            if (nozzleDetachedVisual != null)
                nozzleDetachedVisual.SetActive(!attached);
        }

        private void SetHoseActive(bool active)
        {
            if (hoseLine == null)
                return;

            hoseLine.enabled = active;

            if (active && _activeNozzle != null)
                UpdateHose();
        }

        private void UpdateHose()
        {
            Vector3 start = GetHoseStart();
            Vector3 end = _activeNozzle.transform.position;

            float ratio = Vector3.Distance(start, end) / maxHoseLength;
            float tension = Mathf.InverseLerp(tensionStartRatio, 1f, ratio);
            float slack = Mathf.Lerp(hoseSlack, 0f, tension);

            if (hoseLine.positionCount != hoseSegments)
                hoseLine.positionCount = hoseSegments;

            for (int i = 0; i < hoseSegments; i++)
            {
                float t = i / (float)(hoseSegments - 1);
                Vector3 point = Vector3.Lerp(start, end, t);

                point.y -= Mathf.Sin(t * Mathf.PI) * slack;

                hoseLine.SetPosition(i, point);
            }
        }
    }
}