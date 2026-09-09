using UnityEngine;

namespace DevLib.PolyNavMesh
{
    [CreateAssetMenu(fileName = "PolyNavAgentData", menuName = "Lib/PolyNavMesh/Nav Agent data", order = 10)]
    public class PolyNavAgentDataSo : ScriptableObject
    {
        [field: SerializeField] public float AgentRadius { get; private set; } = 0.5f;
    }
}