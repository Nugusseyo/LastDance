using UnityEngine;

namespace _Works.CJW.Scripts.Customers
{
    /// <summary>
    /// 씬의 모든 틱 대상을 한 곳에서 돌린다.
    /// 손님, 차량, 방문 세션처럼 타입이 달라도 IUpdate / IFixedUpdate만 구현하면 등록된다.
    /// </summary>
    public class AgentManager : MonoBehaviour
    {
        private readonly TickGroup _tickGroup = new();

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
            _tickGroup.Clear();
        }
    }
}
