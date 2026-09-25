using _Works.CJW.Scripts.ManagingAgents;
using DevLib.ModuleSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Interaction
{
    public class ReputationModule : AbstractModule, IUpdate, IReputationModule
    {
        [SerializeField, Tooltip("평판이 감소될 주기.")] private float reduceInterval = 1f;
        [SerializeField, Tooltip("평판을 한번 감소할 때 줄이는 양.")] private float reduceAmount = 5f;
        [SerializeField] private float maxReputation = 100f;
        
        public float Reputation => _currentReputation;
        public float MaxReputation => maxReputation;
        
        private float _currentReputation;

        private float _timer;
        
        public void InitializeReputation()
        {
            _currentReputation = maxReputation;
            _timer = reduceInterval;
        }
        
        public void OnUpdate(float dt)
        {
            _timer -= dt;
            if (_timer <= 0)
            {
                _currentReputation = Mathf.Max(_currentReputation - reduceAmount, 0);
                Debug.Log($"현재 평판: {_currentReputation}");
                if (_currentReputation <= 0)
                {
                    // todo 여기서 평판 0이 됐을 때 처리해야함.
                }
                _timer = reduceInterval;    
            }
        }
    }
}