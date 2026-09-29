using System;
using UnityEngine;

namespace _Works.JYG._Scripts
{
    public class OutBox : MonoBehaviour
    {
        [SerializeField] private Transform spawnTrm;

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                other.transform.position = spawnTrm.position;
            }
        }
    }
}
