using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.CJW.Scripts.Customers.Movement
{
    /// <summary>걸어다니는 에이전트의 이동. 부르는 쪽은 "어디로 가서 도착할 때까지 기다려"까지만 알고,
    /// 루트 모션이든 Agent든 실제로 옮기는 방법은 구현이 정한다.</summary>
    public interface IMover
    {
        /// <summary>지금 이동을 시킬 수 있는지. 탑승 중이거나 NavMesh 밖이면 false다.</summary>
        bool IsReady { get; }

        /// <summary>목적지에 닿았는지. 경로가 아직 계산 중이면 도착으로 보지 않는다.</summary>
        bool IsArrived { get; }

        /// <summary>목적지만 찍어 둔다. 도착까지 기다리려면 <see cref="MoveAndWait"/>를 쓴다.</summary>
        void MoveTo(Vector3 destination);

        /// <summary>이동을 접는다. 경로를 비우고 걷기 연출도 내린다.</summary>
        void Stop();

        /// <summary>목적지로 이동하고 도착할 때까지 기다린다. 모든 대기에 <paramref name="ct"/>를 물려야 반납 후에도 태스크가 계속 도는 일이 없다.</summary>
        UniTask<MoveResult> MoveAndWait(Vector3 destination, float timeout, CancellationToken ct);
    }
}
