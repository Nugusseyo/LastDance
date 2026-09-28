using _Works.Shared.Cars;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Warehouse
{
    public class GarageSender : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform teleportPoint;
        [SerializeField] private Transform carPoint;
        [SerializeField] private float lookDistance = 6f;
        [SerializeField, Range(0f, 1f)] private float aimDotThreshold = 0.6f;

        private Camera playerCamera;

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.gKey.wasPressedThisFrame) return;

            SendToGarage(FindLookingCar());
        }

        public bool SendToGarage(IRemovableCar car)
        {
            if (car == null)
            {
                Debug.Log("[GarageSender] 바라보는 곳에 차가 없어요.");
                return false;
            }

            if (!car.CanRemove)
            {
                Debug.Log($"[GarageSender] {car.GameObject.name}는 아직 버려진 차가 아니라서 못 보내요.");
                return false;
            }

            GameObject garageCar = CopyCarToGarage(car.GameObject);

            if (!car.Remove())
            {
                Destroy(garageCar);
                return false;
            }

            TeleportPlayer();
            Debug.Log($"[GarageSender] {car.GameObject.name}를 차고로 보냈어요.");
            return true;
        }

        private GameObject CopyCarToGarage(GameObject car)
        {
            GameObject holder = new GameObject("Car Copy Holder");
            holder.SetActive(false);

            GameObject copy = Instantiate(car, carPoint.position, carPoint.rotation, holder.transform);
            copy.name = car.name + " (Garage)";

            foreach (MonoBehaviour script in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                DestroyImmediate(script);
            }

            foreach (NavMeshAgent agent in copy.GetComponentsInChildren<NavMeshAgent>(true))
            {
                DestroyImmediate(agent);
            }

            foreach (NavMeshObstacle obstacle in copy.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                DestroyImmediate(obstacle);
            }

            foreach (Animator animator in copy.GetComponentsInChildren<Animator>(true))
            {
                DestroyImmediate(animator);
            }

            foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
            }

            copy.transform.SetParent(null, true);
            copy.transform.SetPositionAndRotation(carPoint.position, carPoint.rotation);
            Destroy(holder);

            PutOnFloor(copy);

            return copy;
        }

        private void PutOnFloor(GameObject car)
        {
            Renderer[] renderers = car.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer carRenderer in renderers)
            {
                bounds.Encapsulate(carRenderer.bounds);
            }

            Vector3 rayStart = carPoint.position + Vector3.up * 1.5f;
            if (!Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore)) return;

            float lift = hit.point.y - bounds.min.y;
            car.transform.position += Vector3.up * lift;
        }

        private IRemovableCar FindLookingCar()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return null;
            }

            Vector3 eyePosition = playerCamera.transform.position;
            Vector3 lookDirection = playerCamera.transform.forward;

            IRemovableCar bestCar = null;
            float bestDot = aimDotThreshold;

            foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (script is not IRemovableCar car) continue;

                Vector3 toCar = car.GameObject.transform.position - eyePosition;
                if (toCar.sqrMagnitude > lookDistance * lookDistance) continue;

                float dot = Vector3.Dot(lookDirection, toCar.normalized);

                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestCar = car;
                }
            }

            return bestCar;
        }

        private void TeleportPlayer()
        {
            Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();

            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.position = teleportPoint.position;
            }

            player.SetPositionAndRotation(teleportPoint.position, teleportPoint.rotation);
        }
    }
}
