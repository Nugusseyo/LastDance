using UnityEngine;

namespace _Works.KDH._01.Scripts.Car
{
    public class CarSeller : MonoBehaviour
    {
        [SerializeField] private int carBasePrice = 300;
        [SerializeField] private int wheelDefaultPrice = 50;
        [SerializeField] private int RefuelingPrice = 50;
        [SerializeField] private LayerMask wheelLayerMask;

        public int SellCar(GameObject car)
        {
            if (car == null)
            {
                return 0;
            }

            int price = GetCarBasePrice(car);
            price += GetAttachedWheelsPrice(car);

            Destroy(car);

            return price;
        }

        private int GetCarBasePrice(GameObject car)
        {
            PartValue partValue = car.GetComponent<PartValue>();

            if (partValue != null)
            {
                return partValue.SellPrice;
            }
            return carBasePrice;
        }

        private int GetAttachedWheelsPrice(GameObject car)
        {
            int price = 0;
            Transform[] children = car.GetComponentsInChildren<Transform>();

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == car.transform) continue;
                if (((1 << child.gameObject.layer) & wheelLayerMask) == 0) continue;

                price += GetWheelPrice(child.gameObject);
            }

            return price;
        }

        private int GetWheelPrice(GameObject wheel)
        {
            PartValue wheelValue = wheel.GetComponent<PartValue>();

            if (wheelValue != null)
            {
                return wheelValue.SellPrice;
            }
            return wheelDefaultPrice;
        }
    }
}
