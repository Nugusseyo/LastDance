using System.Collections.Generic;
using _Works.Shared.Cars;
using UnityEngine;

namespace _Works.KDH._01.Scripts.Car
{
    public class CarWallMaker : MonoBehaviour
    {
        [SerializeField] private float findInterval = 0.5f;
        [SerializeField] private float bottomGap = 0.2f;

        private readonly Dictionary<GameObject, Rigidbody> walls = new Dictionary<GameObject, Rigidbody>();
        private readonly List<GameObject> removedCars = new List<GameObject>();
        private float nextFindTime;

        private void Update()
        {
            if (Time.time < nextFindTime) return;

            nextFindTime = Time.time + findInterval;
            FindCars();
        }

        private void FixedUpdate()
        {
            removedCars.Clear();

            foreach (KeyValuePair<GameObject, Rigidbody> pair in walls)
            {
                GameObject car = pair.Key;
                Rigidbody wall = pair.Value;

                if (car == null || wall == null)
                {
                    removedCars.Add(car);
                    continue;
                }

                wall.gameObject.SetActive(car.activeInHierarchy);
                wall.MovePosition(car.transform.position);
                wall.MoveRotation(car.transform.rotation);
            }

            foreach (GameObject car in removedCars)
            {
                if (walls.TryGetValue(car, out Rigidbody wall) && wall != null)
                {
                    Destroy(wall.gameObject);
                }

                walls.Remove(car);
            }
        }

        private void FindCars()
        {
            foreach (MonoBehaviour script in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (script is not IRemovableCar removableCar) continue;

                GameObject car = removableCar.GameObject;
                if (walls.ContainsKey(car)) continue;

                walls.Add(car, CreateWall(car));
            }
        }

        private Rigidbody CreateWall(GameObject car)
        {
            GameObject wall = new GameObject(car.name + " Wall");
            wall.transform.SetPositionAndRotation(car.transform.position, car.transform.rotation);
            wall.transform.localScale = car.transform.lossyScale;

            BoxCollider box = wall.AddComponent<BoxCollider>();
            AddBoxLikeCar(box, car.transform, bottomGap);

            Rigidbody body = wall.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            foreach (Collider carCollider in car.GetComponentsInChildren<Collider>(true))
            {
                Physics.IgnoreCollision(box, carCollider);
            }

            return body;
        }

        public static void AddBoxLikeCar(BoxCollider box, Transform car, float bottomGap)
        {
            Bounds bounds = GetLocalBounds(car);

            Vector3 size = bounds.size;
            Vector3 center = bounds.center;
            size.y = Mathf.Max(0.1f, size.y - bottomGap);
            center.y += bottomGap * 0.5f;

            box.size = size;
            box.center = center;
        }

        private static Bounds GetLocalBounds(Transform car)
        {
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (MeshFilter meshFilter in car.GetComponentsInChildren<MeshFilter>())
            {
                if (meshFilter.sharedMesh == null) continue;

                Bounds meshBounds = meshFilter.sharedMesh.bounds;
                Vector3 min = meshBounds.min;
                Vector3 max = meshBounds.max;

                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y,
                        (i & 4) == 0 ? min.z : max.z);

                    Vector3 carPoint = car.InverseTransformPoint(meshFilter.transform.TransformPoint(corner));

                    if (!hasBounds)
                    {
                        bounds = new Bounds(carPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(carPoint);
                    }
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(Vector3.up, new Vector3(2f, 1.5f, 4f));
            }

            return bounds;
        }
    }
}
