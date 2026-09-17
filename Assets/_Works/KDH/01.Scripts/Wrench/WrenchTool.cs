using UnityEngine;

namespace _Works.KDH._01.Scripts.Wrench
{
    public class WrenchTool : MonoBehaviour
    {
        [SerializeField] private int level = 1;

        public float GetDetachTime()
        {
            if (level == 1) return 4.5f;
            if (level == 2) return 3.5f;
            if (level == 3) return 2.5f;

            return 4.5f;
        }
    }
}
