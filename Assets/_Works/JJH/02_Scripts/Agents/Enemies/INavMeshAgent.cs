using UnityEngine;

namespace JJH._02_Scripts.Agents.Enemies
{
    public interface INavMeshAgent
    {
        void MoveTo(Vector3 targetPosition);
        void StopImmediately();
    }
}