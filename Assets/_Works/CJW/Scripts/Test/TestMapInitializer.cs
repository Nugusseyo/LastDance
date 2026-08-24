using System;
using _Works.CJW.Scripts.Customers.Data;
using UnityEngine;

namespace _Works.CJW.Scripts.Test
{
    public class TestMapInitializer : MonoBehaviour
    {
        [SerializeField] private MapDataSo mapDataSo;
        [SerializeField] private ParkingSlot[] slots;

        private void Awake()
        {
            foreach (var slot in slots)
            {
                mapDataSo.AddParkingSlot(slot);
            }
        }
    }
}