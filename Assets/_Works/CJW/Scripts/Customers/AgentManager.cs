using DevLib.EventChannelSystem;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers
{
    /// <summary>
    /// 씬의 모든 틱 대상을 한 곳에서 돌린다.
    /// 손님, 차량, 방문 세션처럼 타입이 달라도 IUpdate / IFixedUpdate만 구현하면 등록된다.
    /// </summary>
    public class AgentManager : MonoBehaviour
    {
        [Tooltip("등록/해제 요청이 오가는 이벤트 채널.")]
        [SerializeField] private EventChannelSO agentChannel;

        private readonly TickGroup _tickGroup = new();

        private void Awake()
        {
            if (agentChannel == null)
            {
                Debug.LogError("[AgentManager] 이벤트 채널(EventChannelSO)을 지정해야 합니다.", this);
                return;
            }

            agentChannel.AddListener<RegisterAgentEvent>(HandleRegisterAgent);
            agentChannel.AddListener<UnRegisterAgentEvent>(HandleUnRegisterAgent);
        }

        private void HandleRegisterAgent(RegisterAgentEvent evt)
        {
            Register(evt.Target);
        }

        private void HandleUnRegisterAgent(UnRegisterAgentEvent evt)
        {
            UnRegister(evt.Target);
        }

        public void Register(object target)
        {
            _tickGroup.Register(target);
        }

        public void UnRegister(object target)
        {
            _tickGroup.Unregister(target);
        }

        private void Update()
        {
            _tickGroup.Update(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _tickGroup.FixedUpdate(Time.fixedDeltaTime);
        }

        private void OnDestroy()
        {
            if (agentChannel != null)
            {
                agentChannel.RemoveListener<RegisterAgentEvent>(HandleRegisterAgent);
                agentChannel.RemoveListener<UnRegisterAgentEvent>(HandleUnRegisterAgent);
            }

            _tickGroup.Clear();
        }
    }
}
