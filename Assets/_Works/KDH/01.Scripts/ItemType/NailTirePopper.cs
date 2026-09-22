using _Works.KDH._01.Scripts.Wrench;
using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class NailTirePopper : MonoBehaviour
    {
        [SerializeField] private LayerMask wheelLayerMask;
        [SerializeField] private float popForce = 5f;

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & wheelLayerMask) == 0) return;

            GameObject wheel = other.gameObject;

            CarStraightMover car = wheel.GetComponentInParent<CarStraightMover>();
            if (car != null)
            {
                car.Stop();
            }

            WheelPopper.Pop(wheel, popForce);

            Destroy(gameObject);
        }
    }
}
