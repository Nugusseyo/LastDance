using DevLib.ModuleSystem;
using JJH._02_Scripts.Agents.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace Assets._Works.JJH._02_Scripts.Agents.Enemies
{
    public class EnemyNavMeshAgentModule : AbstractModule, INavMeshAgent
    {
        private NavMeshAgent _navMeshAgent;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _navMeshAgent = GetComponentInParent<NavMeshAgent>();
            _navMeshAgent.speed = 7f;
            _navMeshAgent.acceleration = 1000f;
            _navMeshAgent.updateRotation = false;
            _navMeshAgent.autoBraking = false;
        }

        public void MoveTo(Vector3 targetPosition)
        {
            if (_navMeshAgent == null || !_navMeshAgent.isActiveAndEnabled ||
                !_navMeshAgent.isOnNavMesh)
                return;

            _navMeshAgent.SetDestination(targetPosition);
        }

        public void StopImmediately()
        {
            if (_navMeshAgent == null || !_navMeshAgent.isActiveAndEnabled ||
             !_navMeshAgent.isOnNavMesh)
                return;

            _navMeshAgent.ResetPath();
            _navMeshAgent.velocity = Vector3.zero;
        }
    }
}