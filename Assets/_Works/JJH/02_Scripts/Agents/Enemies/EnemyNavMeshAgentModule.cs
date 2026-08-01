using DevLib.ModuleSystem;
using JJH._02_Scripts.Agents.Enemies;
using UnityEngine;
using UnityEngine.AI;

namespace Assets._Works.JJH._02_Scripts.Agents.Enemies
{
    public class EnemyNavMeshAgentModule : AbstractModule, INavMeshAgent
    {
        private AbstractEnemy _enemyOwner;

        private NavMeshAgent _navMeshAgent;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            _enemyOwner = (AbstractEnemy)_owner;

            _navMeshAgent = GetComponentInParent<NavMeshAgent>();
            _navMeshAgent.speed = 7f;
            _navMeshAgent.acceleration = 1000f;
            _navMeshAgent.autoBraking = false;
        }

        public void MoveTo(Vector3 targetPos)
        {
            if (_navMeshAgent == null || !_navMeshAgent.isActiveAndEnabled ||
                !_navMeshAgent.isOnNavMesh)
                return;

            _navMeshAgent.SetDestination(targetPos);
        }

        public void MoveToFixedPos(Vector3 targetPos, Vector3 fixedPos)
        {
            if (_navMeshAgent == null || !_navMeshAgent.isActiveAndEnabled ||
                !_navMeshAgent.isOnNavMesh || _enemyOwner.Renderer == null)
                return;

            _navMeshAgent.Warp(targetPos);
            _enemyOwner.Renderer.SetVisualPos(fixedPos);

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