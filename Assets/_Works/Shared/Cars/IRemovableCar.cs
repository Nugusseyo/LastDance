using UnityEngine;

namespace _Works.Shared.Cars
{
    /// <summary>치울 수 있는 차. 손님이 버리고 간 차(다른 차를 훔쳐 달아났거나, 탄 사람이 모두 죽은 차)가 여기에 해당한다.
    /// 치우는 쪽은 차의 콜라이더에서 <c>GetComponentInParent&lt;IRemovableCar&gt;()</c>로 이걸 찾아 <see cref="Remove"/>만 부른다 —
    /// 차가 풀로 돌아가고 차지하던 주차 자리가 비는 뒷정리는 차 쪽이 한다.</summary>
    /// <example><code>
    /// IRemovableCar car = hit.collider.GetComponentInParent&lt;IRemovableCar&gt;();
    /// if (car != null &amp;&amp; car.CanRemove)
    /// {
    ///     car.Remove();
    /// }
    /// </code></example>
    public interface IRemovableCar
    {
        /// <summary>지금 치울 수 있는지. 버려진 차만 true다 — 손님을 태우고 방문 중인 차는 치울 수 없다.</summary>
        bool CanRemove { get; }

        /// <summary>차의 게임 오브젝트. 치우기 전에 위치를 보거나 연출을 붙일 때 쓴다.</summary>
        GameObject GameObject { get; }

        /// <summary>차를 치운다. 치웠으면 true, <see cref="CanRemove"/>가 false라 아무것도 하지 않았으면 false.</summary>
        bool Remove();
    }
}
