using System.Threading;
using Cysharp.Threading.Tasks;
using DevLib.AnimatorSystem;

namespace _Works.CJW.Scripts.Customers.Animation
{
    /// <summary>걷기·서기가 아닌 '연출' 애니메이션을 트는 쪽. 춤이든 주먹질이든 부르는 쪽은 "이 클립을 이만큼 틀어"까지만 알고,
    /// Animator를 어떻게 다루는지는 구현이 정한다.
    /// 이동 모듈은 <see cref="IsPlaying"/>이 켜져 있는 동안 걷기·서기 클립을 덮어쓰지 않는다 —
    /// 이 약속이 없으면 이동 모듈과 연출이 매 프레임 서로의 클립을 밀어내 손님이 부들거린다.</summary>
    public interface IActionAnimator
    {
        /// <summary>연출이 화면을 잡고 있는 동안 true.</summary>
        bool IsPlaying { get; }

        /// <summary>클립을 틀고 <see cref="End"/>를 부를 때까지 유지한다. 같은 클립을 다시 넣으면 처음부터 다시 튼다 —
        /// 주먹질처럼 한 동작을 여러 번 반복할 때 이걸 쓴다.</summary>
        void Begin(HashDataSO clip);

        /// <summary>연출을 접고 화면을 이동 모듈에 돌려준다. 시작한 쪽이 반드시 짝을 맞춰 불러야 한다.</summary>
        void End();

        /// <summary>클립을 틀고 <paramref name="duration"/>초 뒤에 스스로 접는다. 취소되어도 반드시 접는다.
        /// <paramref name="duration"/>이 0 이하면 취소될 때까지 유지한다.</summary>
        UniTask PlayFor(HashDataSO clip, float duration, CancellationToken ct);
    }
}
