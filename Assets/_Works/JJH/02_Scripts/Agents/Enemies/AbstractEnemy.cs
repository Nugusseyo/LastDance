using System.Diagnostics;
using Unity.Behavior;

namespace JJH._02_Scripts.Agents.Enemies
{
    public class AbstractEnemy : Agent
    {
        public INavMeshAgent EnemyNavMeshAgent { get; private set; }
        public BehaviorGraphAgent BehaviorAgent { get; private set; }

        public bool IsStunned { get; set; } = false;

        protected override void InitializeComponents()
        {
            base.InitializeComponents();

            EnemyNavMeshAgent = GetModule<INavMeshAgent>();
            Debug.Assert(EnemyNavMeshAgent != null, $"{gameObject.name}에는 INavMeshAgent모듈이 필요합니다.");
            BehaviorAgent = GetComponent<BehaviorGraphAgent>();
            Debug.Assert(EnemyNavMeshAgent != null, $"{gameObject.name}에는 BehaviorGraphAgent가 필요합니다.");

            BehaviorAgent.SetVariableValue("Enemy", this);
        }
    }
}
