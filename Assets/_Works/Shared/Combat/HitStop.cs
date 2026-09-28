using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Works.Shared.Combat
{
    /// <summary>맞은 순간 게임 전체를 아주 잠깐 거의 멈춘다(히트스톱). 타격이 몸에 '박히는' 느낌을 만든다.
    /// 멈추는 동안 또 걸리면 더 늦게 끝나는 쪽으로 늘리기만 하고, 끝나면 멈추기 전의 시간 배율로 되돌린다.</summary>
    public static class HitStop
    {
        private static bool _running;
        private static float _until;
        private static float _restoreTo;
        private static float _applied;

        // 도메인 리로드를 끈 채 플레이를 멈추면 기다리던 태스크가 끝나지 못해 _running이 true로 남는다. 다음 플레이에서 풀어 둔다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _running = false;
        }

        /// <summary><paramref name="duration"/>(실제 초) 동안 시간 배율을 지금의 <paramref name="scale"/>배로 낮춘다.</summary>
        public static void Freeze(float duration, float scale)
        {
            if (duration <= 0f || !Application.isPlaying)
            {
                return;
            }

            float until = Time.unscaledTime + duration;
            if (_running)
            {
                _until = Mathf.Max(_until, until);
                return;
            }

            _running = true;
            _until = until;
            _restoreTo = Time.timeScale;

            // 지금 배율 기준으로 낮춘다. 테스트처럼 시간을 빠르게 돌리는 중이어도 되돌릴 때 그 배율로 돌아간다.
            _applied = _restoreTo * Mathf.Clamp01(scale);
            Time.timeScale = _applied;

            Wait().Forget();
        }

        private static async UniTaskVoid Wait()
        {
            while (Time.unscaledTime < _until)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // 멈춘 사이 누가 시간 배율을 바꿨으면(일시정지 메뉴 등) 그쪽을 따른다. 덮어쓰면 일시정지가 풀려 버린다.
            if (Mathf.Approximately(Time.timeScale, _applied))
            {
                Time.timeScale = _restoreTo;
            }

            _running = false;
        }
    }
}
