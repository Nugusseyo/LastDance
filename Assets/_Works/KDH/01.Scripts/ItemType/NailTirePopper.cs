using UnityEngine;

namespace _Works.KDH._01.Scripts.ItemType
{
    public class NailTirePopper : MonoBehaviour
    {
        [SerializeField] private LayerMask wheelLayerMask;

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & wheelLayerMask) == 0) return;

            CarStraightMover car = other.GetComponentInParent<CarStraightMover>();
            if (car != null)
            {
                car.Stop();
            }

            Destroy(gameObject);
        }
    }
}
