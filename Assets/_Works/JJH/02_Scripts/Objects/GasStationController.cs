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

        public bool IsDetached { get; private set; }

        private FuelNozzle _activeNozzle;

        private void Awake()
        {
            SetVisual(true);
            SetHoseActive(false);
        }

        private void LateUpdate()
        {
            if (!IsDetached || hoseLine == null || _activeNozzle == null)
                return;

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

            _activeNozzle = null;
            IsDetached = false;

            SetVisual(true);
            SetHoseActive(false);

            return true;
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

            if (active)
                UpdateHose();
        }

        private void UpdateHose()
        {
            Vector3 start = hoseStartPoint != null ? hoseStartPoint.position : transform.position;
            Vector3 end = _activeNozzle.transform.position;

            if (hoseLine.positionCount != hoseSegments)
                hoseLine.positionCount = hoseSegments;

            for (int i = 0; i < hoseSegments; i++)
            {
                float t = i / (float)(hoseSegments - 1);
                Vector3 point = Vector3.Lerp(start, end, t);

                float sag = Mathf.Sin(t * Mathf.PI) * hoseSlack;
                point.y -= sag;

                hoseLine.SetPosition(i, point);
            }
        }
    }
}