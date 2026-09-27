using UnityEngine;

namespace _Works.KDH._01.Scripts.Car
{
    public class ScrapCarData : MonoBehaviour
    {
        [SerializeField] private int carPrice = 300;
        [SerializeField] private int wheelPrice = 50;
        [SerializeField] private Transform wheelParent;
        [SerializeField] private LayerMask wheelLayer;

        private int startWheelCount;

        public int CarPrice => carPrice;
        public int WheelPrice => wheelPrice;

        private void Awake()
        {
            startWheelCount = CountWheels();
        }

        public int CountRemovedWheels()
        {
            return startWheelCount - CountWheels();
        }

        private int CountWheels()
        {
            int count = 0;

            foreach (Transform child in wheelParent.GetComponentsInChildren<Transform>())
            {
                if (child == wheelParent) continue;

                if ((wheelLayer.value & (1 << child.gameObject.layer)) != 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
