using UnityEngine;

namespace JJH._02_Scripts.Agents.Enemies
{
    public interface INavMeshAgent
    {
        void MoveTo(Vector3 targetPos);
        public void MoveToFixedPos(Vector3 targetPos, Vector3 fixedPos);
        void StopImmediately();
    }
}