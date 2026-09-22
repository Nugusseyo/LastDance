using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Movement
{
    /// <summary>
    /// OnAnimatorMove는 Animator와 같은 GameObject에 붙은 스크립트에서만 불린다.
    /// 손님은 Animator가 visual 자식에 있고 이동 모듈은 몸통에 붙으므로,
    /// 이 중계 컴포넌트가 런타임에 Animator 쪽에 붙어 루트 모션을 넘긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RootMotionRelay : MonoBehaviour
    {
        private IRootMotionReceiver _receiver;

        public void Bind(IRootMotionReceiver receiver)
        {
            _receiver = receiver;
        }

        private void OnAnimatorMove()
        {
            // 이 콜백이 있는 순간 애니메이터는 루트 모션을 스스로 적용하지 않는다.
            // 여기서 넘기지 않으면 애니메이션은 도는데 아무도 움직이지 않는다.
            _receiver?.ApplyRootMotion();
        }
    }
}
