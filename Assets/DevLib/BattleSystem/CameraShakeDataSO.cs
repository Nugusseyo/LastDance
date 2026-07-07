using UnityEngine;

namespace DevLib.BattleSystem
{
    [CreateAssetMenu(fileName = "CameraShakeData", menuName = "System/CameraShakeData", order = 60)]
    public class CameraShakeDataSO : ScriptableObject
    {
        [Tooltip("임펄스 강도")]
        public float force = 1f;

        [Tooltip("임펄스 방향 (단위 벡터 권장)")]
        public Vector3 direction = Vector3.up;
    }
}
