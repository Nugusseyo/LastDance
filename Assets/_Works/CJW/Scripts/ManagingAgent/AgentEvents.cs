using DevLib.EventChannelSystem;

namespace _Works.CJW.Scripts.ManagingAgent
{
    /// <summary>
    /// 틱 등록/해제 이벤트의 공용 인스턴스. 매 프레임 new를 피하려고 미리 하나씩 만들어 두고 Init으로 재사용한다.
    /// 이벤트는 RaiseEvent 안에서 동기로 처리되므로 인스턴스를 돌려써도 안전하다.
    /// </summary>
    public static class AgentEvents
    {
        public static readonly RegisterAgentEvent RegisterAgentEvent = new RegisterAgentEvent();
        public static readonly UnRegisterAgentEvent UnRegisterAgentEvent = new UnRegisterAgentEvent();
    }

    /// <summary>AgentManager에 틱 대상을 등록해 달라는 요청.</summary>
    public class RegisterAgentEvent : GameEvent
    {
        public object Target;

        public RegisterAgentEvent Init(object target)
        {
            Target = target;
            return this;
        }
    }

    /// <summary>AgentManager에서 틱 대상을 빼 달라는 요청.</summary>
    public class UnRegisterAgentEvent : GameEvent
    {
        public object Target;

        public UnRegisterAgentEvent Init(object target)
        {
            Target = target;
            return this;
        }
    }
}
