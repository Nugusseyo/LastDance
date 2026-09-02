using DevLib.ModuleSystem;
using UnityEngine;
using UnityEngine.AI;

namespace _Works.Shared.Boarding
{
    /// <summary>NavMesh로 걸어다니는 에이전트(손님)의 탑승.</summary>
    public sealed class NavMeshBoardingModule : BoardingModule
    {
        [Tooltip("비워두면 소유자 계층에서 찾아 쓴다.")]
        [SerializeField] private NavMeshAgent agent;

        public override void Initialize(ModuleOwner owner)
        {
            base.Initialize(owner);

            if (agent == null && owner != null)
            {
                agent = owner.GetComponentInChildren<NavMeshAgent>();
            }
        }

        protected override void OnBoarded()
        {
            if (agent == null)
            {
                return;
            }

            // 차에 타고 있는 동안에는 NavMesh 위에 없다.
            agent.enabled = false;
        }

        protected override void OnUnboarded(Vector3 landingPosition)
        {
            if (agent == null)
            {
                return;
            }

            agent.enabled = true;

            // 좌석에 붙어 있는 동안 NavMesh 밖으로 나갔을 수 있으므로 현재 위치로 맞춰준다.
            agent.Warp(landingPosition);
        }
    }
}
