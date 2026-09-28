using CustomerCar = _Works.CJW.Scripts.Cars.Car;
using _Works.JYG._Scripts.Events;
using _Works.KDH._01.Scripts.Car;
using _Works.Shared.Cars;
using DevLib.EventChannelSystem;
using Resources.DataBase.Human_Data;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace _Works.KDH._01.Scripts.Warehouse
{
    public class GarageSender : MonoBehaviour
    {
        [Header("Points")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform teleportPoint;
        [SerializeField] private Transform carPoint;

        [Header("Look")]
        [SerializeField] private float lookDistance = 4f;
        [SerializeField] private float carPadding = 0.5f;
        [SerializeField] private float playerSize = 0.6f;

        [Header("Money")]
        [SerializeField] private EventChannelSO moneyChannel;
        [SerializeField] private int carPrice = 300;
        [SerializeField] private int wheelPrice = 50;

        [Header("Tilt")]
        [SerializeField] private float tiltPerWheel = 4f;
        [SerializeField] private float sinkPerWheel = 0.06f;
        [SerializeField] private float tiltSpeed = 2f;

        private Camera playerCamera;
        private GameObject garageCar;
        private Vector3 carStartPosition;
        private Vector3 returnPosition;
        private Quaternion returnRotation;
        private float pitch;
        private float roll;
        private float sink;
        private int removedWheelCount;

        public GameObject GarageCar => garageCar;

        private void Update()
        {
            TiltGarageCar();

            if (Keyboard.current == null) return;

            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                SendToGarage(FindLookingCar());
            }

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (IsLookingAt(garageCar)) SellGarageCar();
                else QuickScrap(FindLookingCar());
            }
        }

        public bool SendToGarage(IRemovableCar car)
        {
            if (!CanScrap(car)) return false;

            if (!IsBadCar(car))
            {
                Debug.Log($"[GarageSender] {car.GameObject.name}는 정상 손님 차라서 차고로 못 보내요.");
                return false;
            }

            if (garageCar != null)
            {
                Debug.Log("[GarageSender] 차고에 이미 차가 있어요. 먼저 팔아주세요.");
                return false;
            }

            string carName = car.GameObject.name;
            GameObject newCar = CopyCarToGarage(car.GameObject);

            if (!car.Remove())
            {
                Destroy(newCar);
                return false;
            }

            SetGarageCar(newCar);

            returnPosition = player.position;
            returnRotation = player.rotation;
            MovePlayer(GetPositionOutsideCar(teleportPoint.position), teleportPoint.rotation);

            Debug.Log($"[GarageSender] {carName}를 차고로 보냈어요.");
            return true;
        }

        public bool QuickScrap(IRemovableCar car)
        {
            if (!CanScrap(car)) return false;

            string carName = car.GameObject.name;
            if (!car.Remove()) return false;

            PayMoney(0);
            Debug.Log($"[GarageSender] {carName}를 빠른 폐차했어요.");
            return true;
        }

        public void SellGarageCar()
        {
            if (garageCar == null) return;

            PayMoney(removedWheelCount);
            Debug.Log($"[GarageSender] {garageCar.name}를 팔았어요. 뺀 바퀴 {removedWheelCount}개");

            Destroy(garageCar);
            garageCar = null;

            MovePlayer(returnPosition, returnRotation);
        }

        public void RemoveWheel(GameObject wheel)
        {
            if (garageCar == null) return;

            Vector3 wheelPosition = garageCar.transform.InverseTransformPoint(wheel.transform.position);

            roll -= Mathf.Sign(wheelPosition.x) * tiltPerWheel;
            pitch += Mathf.Sign(wheelPosition.z) * tiltPerWheel;
            sink += sinkPerWheel;
            removedWheelCount++;
        }

        private bool CanScrap(IRemovableCar car)
        {
            if (car == null)
            {
                Debug.Log("[GarageSender] 바라보는 곳에 차가 없어요.");
                return false;
            }

            if (!car.CanRemove)
            {
                Debug.Log($"[GarageSender] {car.GameObject.name}는 아직 버려진 차가 아니에요.");
                return false;
            }

            return true;
        }

        private bool IsBadCar(IRemovableCar car)
        {
            CustomerCar customerCar = car.GameObject.GetComponent<CustomerCar>();
            return customerCar != null && customerCar.HumanType == HumanType.Bad;
        }

        private void PayMoney(int wheelCount)
        {
            if (moneyChannel == null) return;

            moneyChannel.RaiseEvent(UIEvents.ScrapEvent.Init(carPrice, wheelCount, wheelPrice));
        }

        private void SetGarageCar(GameObject car)
        {
            garageCar = car;
            carStartPosition = car.transform.position;
            pitch = 0f;
            roll = 0f;
            sink = 0f;
            removedWheelCount = 0;
        }

        private GameObject CopyCarToGarage(GameObject car)
        {
            GameObject holder = new GameObject("Car Copy Holder");
            holder.SetActive(false);

            GameObject copy = Instantiate(car, carPoint.position, carPoint.rotation, holder.transform);
            copy.name = car.name + " (Garage)";

            RemoveMovingParts(copy);

            copy.transform.SetParent(null, true);
            copy.transform.SetPositionAndRotation(carPoint.position, carPoint.rotation);
            Destroy(holder);

            BoxCollider wall = copy.AddComponent<BoxCollider>();
            CarWallMaker.AddBoxLikeCar(wall, copy.transform, 0.2f);

            return copy;
        }

        private void RemoveMovingParts(GameObject car)
        {
            foreach (MonoBehaviour script in car.GetComponentsInChildren<MonoBehaviour>(true))
            {
                DestroyImmediate(script);
            }

            foreach (NavMeshAgent agent in car.GetComponentsInChildren<NavMeshAgent>(true))
            {
                DestroyImmediate(agent);
            }

            foreach (NavMeshObstacle obstacle in car.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                DestroyImmediate(obstacle);
            }

            foreach (Animator animator in car.GetComponentsInChildren<Animator>(true))
            {
                DestroyImmediate(animator);
            }

            foreach (Rigidbody body in car.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
            }
        }

        private void TiltGarageCar()
        {
            if (garageCar == null) return;

            Quaternion targetRotation = carPoint.rotation * Quaternion.Euler(pitch, 0f, roll);
            Vector3 targetPosition = carStartPosition + Vector3.down * sink;

            float step = tiltSpeed * Time.deltaTime;
            garageCar.transform.rotation = Quaternion.Slerp(garageCar.transform.rotation, targetRotation, step);
            garageCar.transform.position = Vector3.Lerp(garageCar.transform.position, targetPosition, step);
        }

        private IRemovableCar FindLookingCar()
        {
            if (!FindCamera()) return null;

            IRemovableCar closestCar = null;
            float closestDistance = lookDistance;

            foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (script is not IRemovableCar car) continue;

                if (GetLookDistance(car.GameObject, out float distance) && distance <= closestDistance)
                {
                    closestDistance = distance;
                    closestCar = car;
                }
            }

            return closestCar;
        }

        private bool IsLookingAt(GameObject car)
        {
            if (car == null || !FindCamera()) return false;

            return GetLookDistance(car, out float distance) && distance <= lookDistance;
        }

        private bool GetLookDistance(GameObject car, out float distance)
        {
            Bounds carBounds = GetCarBounds(car);
            carBounds.Expand(carPadding);

            Ray lookRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            return carBounds.IntersectRay(lookRay, out distance);
        }

        private bool FindCamera()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            return playerCamera != null;
        }

        private Bounds GetCarBounds(GameObject car)
        {
            Renderer[] renderers = car.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(car.transform.position, Vector3.one * 2f);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer carRenderer in renderers)
            {
                bounds.Encapsulate(carRenderer.bounds);
            }

            return bounds;
        }

        private void MovePlayer(Vector3 position, Quaternion rotation)
        {
            Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();

            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector3.zero;
                playerRigidbody.position = position;
            }

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            player.SetPositionAndRotation(position, rotation);

            if (controller != null) controller.enabled = true;
        }

        private Vector3 GetPositionOutsideCar(Vector3 position)
        {
            if (garageCar == null) return position;

            Bounds carBounds = GetCarBounds(garageCar);
            carBounds.Expand(playerSize * 2f);

            Vector3 flatPosition = new Vector3(position.x, carBounds.center.y, position.z);
            if (!carBounds.Contains(flatPosition)) return position;

            float toLeft = position.x - carBounds.min.x;
            float toRight = carBounds.max.x - position.x;
            float toBack = position.z - carBounds.min.z;
            float toFront = carBounds.max.z - position.z;
            float shortest = Mathf.Min(Mathf.Min(toLeft, toRight), Mathf.Min(toBack, toFront));

            if (shortest == toLeft) position.x = carBounds.min.x;
            else if (shortest == toRight) position.x = carBounds.max.x;
            else if (shortest == toBack) position.z = carBounds.min.z;
            else position.z = carBounds.max.z;

            return position;
        }
    }
}