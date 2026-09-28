using _Works.JJH._02_Scripts.Items;
using _Works.JYG._Scripts.Events;
using _Works.KDH._01.Scripts.Warehouse;
using _Works.KDH._01.Scripts.Wrench;
using DevLib.EventChannelSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.KDH._01.Scripts.Car
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

        [Header("Detach Time / UI")]
        [SerializeField] private EventChannelSO durationChannel;
        [SerializeField] private float handDetachTime = 8f;

        [Header("Garage")]
        [SerializeField] private GarageSender garageSender;

        public LayerMask PartLayerMask => partLayerMask;
        public bool IsDetaching => _pendingPart != null;

        private Coroutine _collapseCoroutine;

        private GrabItem _pendingPart;
        private float _holdTime;
        private bool _progressShown;

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

            if (part.transform.parent == null)
                return true;

            if (IsWheel(part.gameObject) && !IsInGarage(part.transform))
                return false;

            if (_pendingPart != part)
            {
                _pendingPart = part;
                _holdTime = 0f;
            }

            return false;
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

        private bool IsInGarage(Transform part)
        {
            if (garageSender == null || garageSender.GarageCar == null)
                return false;

            return part.IsChildOf(garageSender.GarageCar.transform);
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

        public void CancelDetach()
        {
            if (_pendingPart == null)
                return;

            _pendingPart = null;
            _holdTime = 0f;
            HideProgress();
        }

        public bool TickDetach(float deltaTime, WrenchTool wrench)
        {
            if (_pendingPart == null)
                return false;

            _holdTime += deltaTime;
            float requiredTime = wrench != null ? wrench.GetDetachTime() : handDetachTime;

            ShowProgress(_holdTime, requiredTime);

            if (_holdTime < requiredTime)
                return false;

            GrabItem part = _pendingPart;
            _pendingPart = null;
            _holdTime = 0f;
            HideProgress();

            DetachNow(part);
            return true;
        }

        private void DetachNow(GrabItem part)
        {
            GameObject partObject = part.gameObject;
            Transform car = partObject.transform.parent;

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
        }

        private void ShowProgress(float current, float max)
        {
            _progressShown = true;
            if (durationChannel == null) return;
            durationChannel.RaiseEvent(UIEvents.DurationEvent.Init(current, max));
        }

        private void HideProgress()
        {
            if (!_progressShown) return;
            _progressShown = false;
            if (durationChannel == null) return;
            durationChannel.RaiseEvent(UIEvents.DurationEvent.Init(1f, 1f));
        }
    }
}