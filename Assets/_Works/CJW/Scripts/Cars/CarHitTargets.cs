using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Works.CJW.Scripts.Cars
{
    /// <summary>치일 수 있는 것들의 등록소. <see cref="CarTraffic"/>이 차를 모으듯 사람을 모은다.
    /// 치임이 일어날 때마다 <see cref="Hit"/>을 내보내, 점수·연출·테스트 도구가 치인 쪽 코드를 몰라도 받아 볼 수 있다.</summary>
    public static class CarHitTargets
    {
        private static readonly List<ICarHittable> _targets = new();

        public static IReadOnlyList<ICarHittable> Targets => _targets;

        /// <summary>치임이 일어났다. 치인 쪽의 <see cref="ICarHittable.OnHitByCar"/>가 먼저 불린 뒤에 나간다.</summary>
        public static event Action<CarHitInfo> Hit;

        public static void Register(ICarHittable target)
        {
            if (target != null && !_targets.Contains(target))
            {
                _targets.Add(target);
            }
        }

        public static void Unregister(ICarHittable target)
        {
            _targets.Remove(target);
        }

        public static void Raise(CarHitInfo hit)
        {
            hit.Target.OnHitByCar(hit);
            Hit?.Invoke(hit);
        }

        // 도메인 리로드를 끈 에디터에서는 static이 플레이 사이에 남는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _targets.Clear();
            Hit = null;
        }
    }
}
