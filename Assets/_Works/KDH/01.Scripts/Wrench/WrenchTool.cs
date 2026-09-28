using UnityEngine;

namespace _Works.KDH._01.Scripts.Wrench
{
    public class WrenchTool : MonoBehaviour
    {
        private const float HandDetachTime = 6f;

        [SerializeField] private int level = 1;

        public float GetDetachTime()
        {
            if (level == 2) return HandDetachTime / 1.5f;
            if (level == 3) return HandDetachTime / 2.1f;

            return 4.5f;
        }
    }
}
