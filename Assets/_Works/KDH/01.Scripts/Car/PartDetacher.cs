using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.JJH._02_Scripts.Agents.Players.Attacks.Weapons
{
    public class PartDetacher : MonoBehaviour
    {
        [Header("Layer")]
        [SerializeField] private LayerMask partLayerMask;
        [SerializeField] private LayerMask wheelLayerMask;
        [SerializeField] private LayerMask groundLayerMask;

        [Header("Detach")]
        [SerializeField] private float popForce = 5f;
        [SerializeField] private float upForce = 3f;
        [SerializeField] private float collapseTiltAngle = 5f;
        [SerializeField] private float collapseDuration = 0.15f;
        [SerializeField] private float pickupDistance = 4f;

        public LayerMask PartLayerMask => partLayerMask;

        private Coroutine _collapseCoroutine;

        private struct WheelSocket
        {
            public Transform parent;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public float carHeight;
        }

        private readonly Dictionary<GameObject, WheelSocket> _wheelSockets = new();

        public bool TryDetachPart(GrabItem part)
        {
            if (part == null)
                return false;

            GameObject partObject = part.gameObject;
            Transform car = partObject.transform.parent;

            if (car == null)
                return false;

            bool isWheel = IsWheel(partObject);
            Vector3 wheelDropPoint = partObject.transform.position;

            if (isWheel && !_wheelSockets.ContainsKey(partObject))
            {
                _wheelSockets[partObject] = new WheelSocket
                {
                    parent = car,
                    localPosition = partObject.transform.localPosition,
                    localRotation = partObject.transform.localRotation,
                    carHeight = car.position.y
                };
            }

            partObject.transform.SetParent(null);

            part.SetPhysicsState();

            Vector3 outward = partObject.transform.position - car.position;
            outward.y = 0f;

            if (outward.sqrMagnitude > 0.001f)
                outward.Normalize();

            part.AddForce(outward * popForce + Vector3.up * upForce, ForceMode.VelocityChange);

            if (isWheel)
                CollapseCar(car, wheelDropPoint);

            return true;
        }

        public bool TryAttachWheel(GrabItem part, Transform cameraTrm)
        {
            if (!_wheelSockets.TryGetValue(part.gameObject, out WheelSocket socket)
                || socket.parent == null || part == null || cameraTrm == null)
                return false;

            float sqrDist = (transform.position - socket.parent.position).sqrMagnitude;
            if (sqrDist > pickupDistance * pickupDistance)
                return false;

            Vector3 socketWorldPosition = socket.parent.TransformPoint(socket.localPosition);
            Vector3 direction = socketWorldPosition - cameraTrm.position;
            if (direction.sqrMagnitude <= 0.001f)
                return false;

            direction.Normalize();

            if (Vector3.Dot(cameraTrm.forward, direction) < 0.95f)
                return false;

            part.SetKinematicState();
            part.transform.SetParent(socket.parent);
            part.transform.SetLocalPositionAndRotation(socket.localPosition, socket.localRotation);

            if (_collapseCoroutine != null)
            {
                StopCoroutine(_collapseCoroutine);
                _collapseCoroutine = null;
            }

            StartCoroutine(RestoreCarRoutine(socket.parent, socket.carHeight));

            return true;
        }

        private bool IsWheel(GameObject partObject)
        {
            return (wheelLayerMask.value & (1 << partObject.layer)) != 0;
        }

        private void CollapseCar(Transform car, Vector3 wheelDropPoint)
        {
            CarStraightMover mover = car.GetComponent<CarStraightMover>();

            if (mover != null)
                mover.Stop();

            if (_collapseCoroutine != null)
                StopCoroutine(_collapseCoroutine);

            _collapseCoroutine = StartCoroutine(CollapseRoutine(car, wheelDropPoint));
        }

        private IEnumerator CollapseRoutine(Transform car, Vector3 wheelDropPoint)
        {
            Vector3 localCorner = car.InverseTransformPoint(wheelDropPoint);
            localCorner.y = 0f;

            Vector3 tiltAxis = Vector3.Cross(Vector3.up, localCorner.normalized);
            Vector3 startPos = car.position;
            Quaternion startRot = car.rotation;

            float groundY = startPos.y;

            Vector3 groundProbe = new Vector3(wheelDropPoint.x, startPos.y + 3f, wheelDropPoint.z);

            if (Physics.Raycast(groundProbe, Vector3.down, out RaycastHit groundHit, 10f, groundLayerMask))
                groundY = groundHit.point.y;

            Vector3 endPos = new Vector3(startPos.x, Mathf.Min(startPos.y, groundY), startPos.z);
            Quaternion endRot = startRot * Quaternion.AngleAxis(collapseTiltAngle, tiltAxis);

            float time = 0f;

            while (time < collapseDuration)
            {
                time += Time.deltaTime;
                float progress = time / collapseDuration;

                car.position = Vector3.Lerp(startPos, endPos, progress);
                car.rotation = Quaternion.Slerp(startRot, endRot, progress);

                yield return null;
            }

            car.position = endPos;
            car.rotation = endRot;
        }

        private IEnumerator RestoreCarRoutine(Transform car, float targetHeight)
        {
            Rigidbody carRigidbody = car.GetComponent<Rigidbody>();
            if (carRigidbody != null)
            {
                carRigidbody.linearVelocity = Vector3.zero;
                carRigidbody.angularVelocity = Vector3.zero;
                carRigidbody.isKinematic = true;
            }

            Vector3 startPosition = car.position;
            Quaternion startRotation = car.rotation;
            Vector3 targetPosition = new Vector3(car.position.x, targetHeight, car.position.z);
            Quaternion targetRotation = Quaternion.Euler(0f, car.eulerAngles.y, 0f);

            float time = 0f;
            while (time < collapseDuration)
            {
                time += Time.deltaTime;
                float progress = time / collapseDuration;

                car.position = Vector3.Lerp(startPosition, targetPosition, progress);
                car.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);

                yield return null;
            }

            car.position = targetPosition;
            car.rotation = targetRotation;

            if (carRigidbody != null)
            {
                carRigidbody.linearVelocity = Vector3.zero;
                carRigidbody.angularVelocity = Vector3.zero;
                carRigidbody.isKinematic = false;
            }

            _collapseCoroutine = null;
        }
    }
}